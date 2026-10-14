using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class WiredBuildApplication : IApplicationFeature
{
    private readonly IApplicationRuntime application;
    private readonly GameState game;
    private readonly IInterceptor interceptor;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly ApplicationEventSource<WiredBuildProgress> progress;
    private readonly Dictionary<int, WiredBuildPacing> placement_pacing = [];
    private WiredBuildPacing? variable_pacing;
    private Scope? pacing_scope;

    public WiredBuildApplication(IApplicationRuntime application, GameState game, IInterceptor interceptor,
        TimeProvider clock, Action<Exception> observer_error)
    {
        this.application = application;
        this.game = game;
        this.interceptor = interceptor;
        this.clock = clock;
        progress = new(observer_error);
        Bindings = [
            new ApplicationCallBinding<WiredBuildGuideRequest, WiredBuildGuide>(WiredBuildDescriptors.Guide,
                (_, _) => ValueTask.FromResult(WiredBuildGuide.Read())),
            new ApplicationCallBinding<WiredBuildRequest, WiredBuildAnalysis>(WiredBuildDescriptors.Analyze,
                (request, _) => ValueTask.FromResult(WiredBuildAnalyzer.Analyze(request))),
            new ApplicationCallBinding<WiredBuildRequest, WiredBuildPreflight>(WiredBuildDescriptors.Preflight, preflight),
            new ApplicationCallBinding<WiredBuildRequest, WiredBuildResult>(WiredBuildDescriptors.Build, build),
            new ApplicationCallBinding<WiredConfigureRequest, WiredFormView>(WiredBuildDescriptors.Configure, configure),
            new ApplicationCallBinding<WiredUsageMeasureRequest, WiredUsageMeasurement>(WiredBuildDescriptors.Measure, measure),
            new ApplicationCallBinding<WiredInspectRequest, WiredInspectResult>(WiredBuildDescriptors.Inspect, inspect),
            new ApplicationEventBinding<WiredBuildProgress>(WiredBuildDescriptors.Progress, progress.Subscribe)
        ];
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public void Dispose()
    {
        lifetime.Cancel();
        progress.Dispose();
    }

    private ValueTask<T> call<TRequest, T>(string id, TRequest request, CancellationToken cancellation) =>
        application.InvokeAsync<TRequest, T>(id, request, cancellation);

    private async ValueTask<WiredBuildPreflight> preflight(WiredBuildRequest request, CancellationToken cancellation)
    {
        request = snapshot(request);
        WiredBuildAnalysis analysis = WiredBuildAnalyzer.Analyze(request);
        if (!WiredBuildAnalyzer.ValidOptions(request.Options ?? new())) throw new ArgumentException("Invalid build options.", nameof(request));
        var scope = capture();
        WiredRoomStats stats = await room_stats(scope, (request.Options ?? new()).TimeoutMilliseconds, cancellation).ConfigureAwait(false);
        current(scope);
        WiredErrorLogs errors = await room_read<WiredErrorLogs>(ApplicationMemberIds.WiredRoomErrorLogsGet,
            scope, (request.Options ?? new()).TimeoutMilliseconds, cancellation).ConfigureAwait(false);
        current(scope);
        var failures = analysis.Errors.ToList();
        var warnings = analysis.Warnings.ToList();
        WiredBuildOptions options = request.Options ?? new();
        if (options.ExpectedRoomId is { } expected_room && expected_room != scope.RoomId ||
            options.ExpectedRoomGeneration is { } expected_generation && expected_generation != scope.Generation)
            failures.Add("The plan's saved room identity no longer matches the active room.");
        if (!game.Wired.Snapshot.CanModify || !game.Wired.Snapshot.CanRead)
            failures.Add("The server has not granted Wired read and modification permissions in this room.");
        var claimed = new HashSet<Id>();
        int missing = 0;
        foreach (WiredBuildItem item in request.Items.Where(item => item is not null && item.X >= 0 && item.Y >= 0 && !string.IsNullOrWhiteSpace(item.ClassName)))
        {
            try
            {
                FurniInfo info = definition(item);
                FloorPlan? floor = game.Room.Capture(room => room.FloorPlan);
                if (floor is not null)
                {
                    int width = item.Direction % 4 == 2 ? info.Length : info.Width;
                    int length = item.Direction % 4 == 2 ? info.Width : info.Length;
                    if ((long)item.X + width > floor.Width || (long)item.Y + length > floor.Length)
                        throw new InvalidOperationException($"{item.Key}: the furniture footprint extends outside the floor.");
                    for (int x = item.X; x < (long)item.X + width; x++)
                        for (int y = item.Y; y < (long)item.Y + length; y++)
                            if (floor[x, y] < 0) throw new InvalidOperationException($"{item.Key}: the furniture footprint extends outside the floor.");
                }
                FloorItem? existing = find(item, info.Kind, claimed);
                if (existing is not null) claimed.Add(existing.Id);
                else if (item.PlaceIfMissing) missing++;
                else failures.Add($"{item.Key}: furniture is missing and placement is disabled.");
            }
            catch (InvalidOperationException ex) { failures.Add(ex.Message); }
        }
        if (missing > 0 && (long)stats.RoomStats.FloorItemCount + missing > stats.RoomStats.FloorItemCap)
            failures.Add($"The plan needs {missing} additional floor items but the reported cap is {stats.RoomStats.FloorItemCap}.");
        if (stats.RoomStats.ExecutionCostCap <= 0 || !double.IsFinite(stats.RoomStats.ExecutionCostCap) || !double.IsFinite(stats.RoomStats.ExecutionCost) ||
            stats.RoomStats.ExecutionCost >= stats.RoomStats.ExecutionCostCap * options.MaximumUsageFraction)
            failures.Add("Current Wired usage leaves insufficient configured headroom.");
        if (stats.RoomStats.IsHeavy) warnings.Add("The room is marked Heavy. Review existing load before adding ongoing work.");
        if (analysis.EstimatedUsage is { } estimate && stats.RoomStats.ExecutionCost + estimate >= stats.RoomStats.ExecutionCostCap * options.MaximumUsageFraction)
            warnings.Add("The supplied workload calibration predicts insufficient usage headroom; validate the calibration before activation.");
        return new(analysis with { Errors = failures, Warnings = warnings }, stats.RoomStats, errors.Errors);
    }

    private async ValueTask<WiredBuildResult> build(WiredBuildRequest request, CancellationToken cancellation)
    {
        request = snapshot(request);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        CancellationToken token = linked.Token;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var scope = capture();
            WiredBuildPreflight check = await preflight(request, token).ConfigureAwait(false);
            var results = new List<WiredBuildItemResult>();
            if (!check.Analysis.Valid) return new(false, scope.RoomId, scope.Generation, results, null, string.Join("\n", check.Analysis.Errors), check);
            WiredBuildOptions options = request.Options ?? new();
            using var dialogs = new DialogScope(interceptor, options.HideSettings);
            using var feedback = new WiredBuildFeedback(game, interceptor, check.Errors);
            if (pacing_scope != scope)
            {
                placement_pacing.Clear();
                variable_pacing = new(clock);
                pacing_scope = scope;
            }
            var ids = new Dictionary<string, Id>(StringComparer.Ordinal);
            HashSet<Id> initial_items = game.Room.Capture(room => room.FloorItems.Select(item => item.Id).ToHashSet());
            var infos = request.Items.ToDictionary(item => item.Key, definition, StringComparer.Ordinal);
            var offers = new Dictionary<int, (int Page, int Offer)>();
            long catalog_generation = 0;
            var claimed = new HashSet<Id>();
            string? active = null;
            try
            {
                foreach (WiredBuildItem item in request.Items)
                {
                    active = item.Key;
                    current(scope);
                    FloorItem? existing = find(item, infos[item.Key].Kind, claimed);
                    if (existing is null) continue;
                    ids.Add(item.Key, existing.Id);
                    claimed.Add(existing.Id);
                    results.Add(new(item.Key, existing.Id, false, false));
                }
                int[] missing_kinds = request.Items.Where(item => !ids.ContainsKey(item.Key)).Select(item => infos[item.Key].Kind).Distinct().ToArray();
                if (missing_kinds.Length > 0)
                {
                    progress.Publish(new("catalog", 0, missing_kinds.Length, null));
                    foreach (int kind in missing_kinds)
                    {
                        if (offer(kind) is { } cached) offers.Add(kind, cached);
                    }
                    if (offers.Count != missing_kinds.Length)
                    {
                        CatalogIndex index = await game.Catalog.GetIndexAsync("BUILDERS_CLUB", Timeout.InfiniteTimeSpan,
                            options.TimeoutMilliseconds, token).ConfigureAwait(false);
                        current(scope);
                        CatalogStateView indexed = await call<CatalogStateRequest, CatalogStateView>(ApplicationMemberIds.CatalogState,
                            new("BUILDERS_CLUB"), token).ConfigureAwait(false);
                        offers.Clear();
                        var nodes = new List<CatalogNode>();
                        var expected_offers = infos.Values.Where(info => missing_kinds.Contains(info.Kind))
                            .Select(info => info.BuildersClubOfferId).Where(id => id > 0).ToHashSet();
                        var pending = new Stack<CatalogNode>();
                        pending.Push(index.Root);
                        while (pending.TryPop(out CatalogNode? node))
                        {
                            if (!node.Visible) continue;
                            if (node.PageId >= 0 && node.OfferIds.Count > 0) nodes.Add(node);
                            for (int child = node.Children.Count - 1; child >= 0; child--) pending.Push(node.Children[child]);
                        }
                        foreach (int kind in missing_kinds)
                            if (offer(kind) is { } cached) offers.Add(kind, cached);
                        foreach (CatalogNode node in nodes.DistinctBy(node => node.PageId)
                            .OrderByDescending(node => node.OfferIds.Any(expected_offers.Contains))
                            .ThenByDescending(node => node.PageName.Contains("wired", StringComparison.OrdinalIgnoreCase)))
                        {
                            if (offers.Count == missing_kinds.Length) break;
                            current(scope);
                            int attempts = node.OfferIds.Any(expected_offers.Contains) ? 2 : 1;
                            for (int attempt = 0; attempt < attempts; attempt++)
                            {
                                try
                                {
                                    await call<CatalogPageGetRequest, CatalogPageView>(ApplicationMemberIds.CatalogPageGet,
                                        new(node.PageId, CatalogType: "BUILDERS_CLUB", MaxAgeMilliseconds: -1,
                                            TimeoutMilliseconds: options.TimeoutMilliseconds,
                                            ExpectedCatalogGeneration: indexed.CatalogGeneration), token).ConfigureAwait(false);
                                    break;
                                }
                                catch (RequestTimeoutException) { }
                                current(scope);
                            }
                            current(scope);
                            foreach (int kind in missing_kinds.Where(kind => !offers.ContainsKey(kind)))
                                if (offer(kind) is { } cached) offers.Add(kind, cached);
                            progress.Publish(new("catalog", offers.Count, missing_kinds.Length, node.PageName));
                        }
                        foreach (int kind in missing_kinds.Where(kind => !offers.ContainsKey(kind)))
                            offers.Add(kind, offer(kind) ?? throw new InvalidOperationException($"No exact Builders Club floor offer for kind {kind}."));
                    }
                    CatalogStateView catalog = await call<CatalogStateRequest, CatalogStateView>(ApplicationMemberIds.CatalogState,
                        new("BUILDERS_CLUB"), token).ConfigureAwait(false);
                    catalog_generation = catalog.CatalogGeneration;
                    foreach (var candidate in offers)
                    {
                        CatalogPageView? page = null;
                        long page_started = clock.GetTimestamp();
                        for (int attempt = 0; attempt < 2; attempt++)
                        {
                            current(scope);
                            int remaining = options.TimeoutMilliseconds - (int)clock.GetElapsedTime(page_started).TotalMilliseconds;
                            if (remaining <= 0)
                                throw new RequestTimeoutException(MessageKeys.Catalog.PageRequest.Value,
                                    MessageKeys.Catalog.PageSnapshot.Value, options.TimeoutMilliseconds);
                            try
                            {
                                page = await call<CatalogPageGetRequest, CatalogPageView>(ApplicationMemberIds.CatalogPageGet,
                                    new(candidate.Value.Page, candidate.Value.Offer, "BUILDERS_CLUB",
                                        TimeoutMilliseconds: attempt == 0 ? Math.Max(1, remaining / 2) : remaining,
                                        ExpectedCatalogGeneration: catalog_generation), token).ConfigureAwait(false);
                                break;
                            }
                            catch (RequestTimeoutException) when (attempt == 0)
                            {
                                progress.Publish(new("retry", 1, 2, $"catalog:{candidate.Value.Page}"));
                            }
                        }
                        current(scope);
                        if (!page!.Offers.Any(entry => entry.OfferId == candidate.Value.Offer && entry.TotalProducts == 1 &&
                            entry.Products.Count == 1 && entry.Products[0].ProductType == "s" && entry.Products[0].FurniClassId == candidate.Key))
                            throw new InvalidOperationException("The Builders Club offer changed. Refresh the catalog and recheck the plan.");
                    }
                }
                foreach (WiredBuildItem item in request.Items.OrderBy(item => item.Category == WiredFormCategory.Trigger))
                {
                    active = item.Key;
                    current(scope);
                    if (ids.ContainsKey(item.Key)) continue;
                    var route = offers[infos[item.Key].Kind];
                    Id id = await place(item, infos[item.Key].Kind, route, catalog_generation, options, scope, feedback, token).ConfigureAwait(false);
                    ids.Add(item.Key, id);
                    results.Add(new(item.Key, id, true, false));
                    current(scope);
                    int direction = game.Room.Capture(room => room.FloorItem(id)?.Direction ?? -1);
                    if (direction != item.Direction)
                        throw new InvalidOperationException($"{item.Key}: furniture {id} was placed, but the server returned direction {direction} instead of {item.Direction}. Use the supported direction and reuse this item; placement was not retried.");
                    progress.Publish(new("placement", ids.Count, request.Items.Count, item.Key));
                    if (ids.Count % 25 == 0) await health(check, options, scope, token).ConfigureAwait(false);
                }
                if (request.Items.Any(item => item.State is not null || item.AltitudeCentimeters is not null))
                {
                    WiredVariableCollectionSnapshot variables = await call<WiredVariableListRequest, WiredVariableCollectionSnapshot>(
                        ApplicationMemberIds.WiredVariablesList, new(TimeoutMilliseconds: options.TimeoutMilliseconds), token).ConfigureAwait(false);
                    foreach (WiredBuildItem item in request.Items)
                    {
                        active = item.Key;
                        Id id = ids[item.Key];
                        if (item.State is { } state)
                            await set_value(id, "@state", state, () => game.Room.Capture(room => room.FloorItem(id)?.State) == state,
                                variables, options, scope, feedback, token).ConfigureAwait(false);
                        if (item.AltitudeCentimeters is { } altitude)
                            await set_value(id, "@altitude", altitude, () => game.Room.Capture(room =>
                                room.FloorItem(id) is { } furni && Math.Abs(furni.Z - altitude / 100.0) < 0.004),
                                variables, options, scope, feedback, token).ConfigureAwait(false);
                    }
                }
                int configured = 0;
                int total = request.Items.Count(item => item.Category is not null);
                WiredVariableCollectionSnapshot? named_variables = null;
                foreach (WiredBuildItem item in request.Items.Where(item => item.Category is not null).OrderBy(item => item.Category switch
                {
                    WiredFormCategory.Variable => 0, WiredFormCategory.Addon => 1, WiredFormCategory.Selector => 2,
                    WiredFormCategory.Condition => 3, WiredFormCategory.Action => 4, _ => 5
                }))
                {
                    active = item.Key;
                    current(scope);
                    feedback.Check();
                    WiredFormPatch settings = item.Settings ?? new();
                    if (item.VariablesByName is { Count: > 0 })
                    {
                        named_variables ??= await call<WiredVariableListRequest, WiredVariableCollectionSnapshot>(
                            ApplicationMemberIds.WiredVariablesList, new(TimeoutMilliseconds: options.TimeoutMilliseconds), token).ConfigureAwait(false);
                        var fields = (settings.Fields ?? []).ToList();
                        foreach (var binding in item.VariablesByName)
                        {
                            WiredVariableSnapshot[] matches = named_variables.Variables.Select(entry => entry.Variable)
                                .Where(variable => variable.VariableName == binding.Value).ToArray();
                            if (matches.Length != 1) throw new InvalidOperationException($"{item.Key}: variable name '{binding.Value}' must resolve uniquely.");
                            fields.Add(new(binding.Key, new(Text: matches[0].VariableId)));
                        }
                        settings = settings with { Fields = fields };
                    }
                    settings = settings with {
                        Furniture = item.FurnitureKeys is null ? settings.Furniture : item.FurnitureKeys.Select(key => ids[key]).ToArray(),
                        SecondaryFurniture = item.SecondaryFurnitureKeys is null ? settings.SecondaryFurniture : item.SecondaryFurnitureKeys.Select(key => ids[key]).ToArray()
                    };
                    current(scope);
                    WiredFormView review = await call<WiredConfigurationGetRequest, WiredFormView>(ApplicationMemberIds.WiredFormGet,
                        new(ids[item.Key], options.TimeoutMilliseconds), token).ConfigureAwait(false);
                    if (review.Definition.Category != item.Category || (review.Definition.Code != item.Code && !review.Definition.Aliases.Contains(item.Code!.Value)))
                        throw new InvalidOperationException($"{item.Key}: received {review.Definition.Name}, not the expected form.");
                    await save_form(review, settings, item.CaptureSnapshot, options, scope, token).ConfigureAwait(false);
                    feedback.Check();
                    if (item.Category == WiredFormCategory.Variable) named_variables = null;
                    int index = results.FindIndex(result => result.Key == item.Key);
                    results[index] = results[index] with { Configured = true };
                    progress.Publish(new("configuration", ++configured, total, item.Key));
                    if (configured % 25 == 0) await health(check, options, scope, token).ConfigureAwait(false);
                }
                verify_items(request, ids, infos, initial_items);
                await health(check, options, scope, token).ConfigureAwait(false);
                verify_items(request, ids, infos, initial_items);
                feedback.Check();
                progress.Publish(new("complete", request.Items.Count, request.Items.Count, null));
                return new(true, scope.RoomId, scope.Generation, results.AsReadOnly(), null, null, check);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress.Publish(new("failed", results.Count(result => result.Configured), request.Items.Count, active));
                return new(false, scope.RoomId, scope.Generation, results.AsReadOnly(), active, ex.Message, check);
            }
        }
        finally { gate.Release(); }
    }

    private async ValueTask<WiredInspectResult> inspect(WiredInspectRequest request, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FurnitureIds);
        if (request.TimeoutMilliseconds is < 1 or > 120000 || request.FurnitureIds.Any(id => id <= 0))
            throw new ArgumentOutOfRangeException(nameof(request));
        request = request with { FurnitureIds = request.FurnitureIds.ToArray() };
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var scope = capture();
            if (!game.Wired.Snapshot.CanRead) throw new InvalidOperationException("The server has not granted Wired read permission.");
            using var dialogs = new DialogScope(interceptor, request.HideSettings);
            var items = new List<WiredInspectItem>();
            foreach (Id id in request.FurnitureIds.Distinct())
            {
                current(scope);
                try
                {
                    WiredFormView form = await call<WiredConfigurationGetRequest, WiredFormView>(ApplicationMemberIds.WiredFormGet,
                        new(id, request.TimeoutMilliseconds), linked.Token).ConfigureAwait(false);
                    current(scope);
                    items.Add(new(id, form, null));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { current(scope); items.Add(new(id, null, ex.Message)); }
                progress.Publish(new("inspection", items.Count, request.FurnitureIds.Count, id.ToString()));
            }
            return new(scope.RoomId, scope.Generation, items.AsReadOnly());
        }
        finally { gate.Release(); }
    }

    private async ValueTask<WiredUsageMeasurement> measure(WiredUsageMeasureRequest request, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DurationMilliseconds is < 100 or > 60000 || request.IntervalMilliseconds is < 100 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(request));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        CancellationToken token = linked.Token;
        var scope = capture();
        int timeout = new WiredTimeoutRequest().TimeoutMilliseconds;
        WiredErrorLogs baseline = await room_read<WiredErrorLogs>(ApplicationMemberIds.WiredRoomErrorLogsGet, scope, timeout, token).ConfigureAwait(false);
        var samples = new List<WiredRoomStatsData>();
        long started = clock.GetTimestamp();
        do
        {
            current(scope);
            WiredRoomStats stats = await room_stats(scope, timeout, token).ConfigureAwait(false);
            current(scope);
            samples.Add(stats.RoomStats);
            if (clock.GetElapsedTime(started).TotalMilliseconds >= request.DurationMilliseconds) break;
            await Task.Delay(TimeSpan.FromMilliseconds(request.IntervalMilliseconds), clock, token).ConfigureAwait(false);
        } while (true);
        WiredErrorLogs final = await room_read<WiredErrorLogs>(ApplicationMemberIds.WiredRoomErrorLogsGet, scope, timeout, token).ConfigureAwait(false);
        current(scope);
        return new(samples[0], samples[^1], samples.Count, samples.Max(sample => sample.ExecutionCost),
            samples.Average(sample => sample.ExecutionCost), samples.Any(sample => sample.IsHeavy),
            final.Errors.Where(error => error.ThrowCount > (baseline.Errors.FirstOrDefault(before => before.ErrorName == error.ErrorName)?.ThrowCount ?? 0)).ToArray());
    }

    private async Task<WiredFormView?> save_form(WiredFormView review, WiredFormPatch settings, bool capture_snapshot,
        WiredBuildOptions options, Scope scope, CancellationToken token)
    {
        WiredFormView? result = review;
        WiredForm form = review.CreateForm();
        settings.Apply(form);
        WiredFormPatch changes = review.CreatePatch(form);
        current(scope);
        if (!empty(changes) || capture_snapshot)
        {
            result = null;
            WiredConfigurationSaveResult saved;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    saved = await call<WiredFormSaveRequest, WiredConfigurationSaveResult>(ApplicationMemberIds.WiredFormSave,
                        new(review.Configuration.Id, review.Generation, review.Revision, changes, options.TimeoutMilliseconds), token).ConfigureAwait(false);
                    break;
                }
                catch (WiredFormReviewException) when (attempt < 2)
                {
                    current(scope);
                    WiredFormView refreshed = await call<WiredConfigurationGetRequest, WiredFormView>(ApplicationMemberIds.WiredFormGet,
                        new(review.Configuration.Id, options.TimeoutMilliseconds), token).ConfigureAwait(false);
                    if (refreshed.Generation != review.Generation || refreshed.Definition.Category != review.Definition.Category ||
                        refreshed.Definition.Code != review.Definition.Code)
                        throw new InvalidOperationException("The Wired form changed while refreshing the reviewed configuration.");
                    review = refreshed;
                    form = review.CreateForm();
                    settings.Apply(form);
                    changes = review.CreatePatch(form);
                }
            }
            if (!saved.Success) throw new InvalidOperationException($"{review.Configuration.Id}: the hotel rejected the configuration: {saved.ValidationError}.");
            if (options.VerifySaves)
            {
                WiredFormView readback = await call<WiredConfigurationGetRequest, WiredFormView>(ApplicationMemberIds.WiredFormGet,
                    new(review.Configuration.Id, options.TimeoutMilliseconds), token).ConfigureAwait(false);
                WiredForm actual = readback.CreateForm();
                settings.Apply(actual);
                WiredFormPatch difference = readback.CreatePatch(actual);
                if (actual is WiredUsersByNameSelectorForm)
                {
                    string? stored = readback.CreateForm().GetField("usernames").Text;
                    difference = difference with { Fields = difference.Fields?.Where(edit =>
                        edit.Name != "usernames" || !string.Equals(edit.Value.Text, stored, StringComparison.OrdinalIgnoreCase)).ToArray() };
                }
                if (actual is WiredVariableLeaderboardAddonForm leaderboard && !leaderboard.ComparisonEnabled &&
                    readback.CreateForm().GetField("literal_operand").Text is "")
                    difference = difference with { Fields = difference.Fields?.Where(edit => edit.Name != "literal_operand").ToArray() };
                if (!empty(difference)) throw new InvalidOperationException($"{review.Configuration.Id}: saved configuration did not match readback.");
                result = readback;
            }
        }
        current(scope);
        return result;
    }

    private async ValueTask<WiredFormView> configure(WiredConfigureRequest request, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Settings);
        if (request.FurniId <= 0 || !WiredBuildAnalyzer.ValidOptions(request.Options ?? new()))
            throw new ArgumentOutOfRangeException(nameof(request));
        request = request with { Settings = snapshot(request.Settings) };
        request.Settings.Apply(WiredFormRegistry.Draft(request.Category, request.Code));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        CancellationToken token = linked.Token;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var scope = capture();
            if (!game.Wired.Snapshot.CanModify || !game.Wired.Snapshot.CanRead)
                throw new InvalidOperationException("The server has not granted Wired read and modification permissions.");
            WiredBuildOptions options = request.Options ?? new();
            if (options.ExpectedRoomId is { } expected_room && expected_room != scope.RoomId ||
                options.ExpectedRoomGeneration is { } expected_generation && expected_generation != scope.Generation)
                throw new InvalidOperationException("The saved room identity no longer matches the active room.");
            using var dialogs = new DialogScope(interceptor, options.HideSettings);
            WiredFormView review = await call<WiredConfigurationGetRequest, WiredFormView>(ApplicationMemberIds.WiredFormGet,
                new(request.FurniId, options.TimeoutMilliseconds), token).ConfigureAwait(false);
            current(scope);
            if (review.Definition.Category != request.Category || review.Definition.Code != request.Code && !review.Definition.Aliases.Contains(request.Code))
                throw new InvalidOperationException("The furniture does not match the requested Wired form.");
            WiredFormView? result = await save_form(review, request.Settings, request.CaptureSnapshot, options, scope, token).ConfigureAwait(false);
            result ??= await call<WiredConfigurationGetRequest, WiredFormView>(ApplicationMemberIds.WiredFormGet,
                new(request.FurniId, options.TimeoutMilliseconds), token).ConfigureAwait(false);
            current(scope);
            return result;
        }
        finally { gate.Release(); }
    }

    private async Task set_value(Id id, string name, int value, Func<bool> matches,
        WiredVariableCollectionSnapshot variables, WiredBuildOptions options, Scope scope, WiredBuildFeedback feedback, CancellationToken token)
    {
        current(scope);
        if (matches()) return;
        WiredVariableSnapshot variable = variables.Variables.Select(entry => entry.Variable).SingleOrDefault(variable =>
            variable.VariableName == name && variable.VariableTarget == (int)WiredTarget.Furni && variable.CanWriteValue && variable.HasValue)
            ?? throw new InvalidOperationException($"No writable furniture {name} variable is available.");
        WiredBuildPacing pacing = variable_pacing ??= new(clock);
        long started = clock.GetTimestamp();
        const int maximum_attempts = 3;
        for (int attempt = 0; attempt < maximum_attempts; attempt++)
        {
            current(scope);
            feedback.Check();
            if (matches()) return;
            int remaining = options.TimeoutMilliseconds - (int)clock.GetElapsedTime(started).TotalMilliseconds;
            if (remaining <= 0) break;
            long revision = feedback.Throttles;
            int pause = (int)Math.Ceiling(pacing.RemainingSpacing().TotalMilliseconds);
            if (pause > 0)
            {
                int recovery_budget = Math.Max(1, remaining / (maximum_attempts - attempt + 1));
                await confirmation(matches, Math.Min(pause, recovery_budget), scope, feedback, revision, token, false).ConfigureAwait(false);
                if (matches()) return;
                remaining = options.TimeoutMilliseconds - (int)clock.GetElapsedTime(started).TotalMilliseconds;
                if (remaining <= 0) break;
            }
            pacing.Sent();
            await call<WiredObjectVariableSetRequest, WiredDispatchResult>(ApplicationMemberIds.WiredVariablesObjectSet,
                new(WiredTarget.Furni, checked((int)(long)id), variable.VariableId, value), token).ConfigureAwait(false);
            int response_budget = Math.Max(1, remaining / (2 * (maximum_attempts - attempt) - 1));
            BuildConfirmation result = await confirmation(matches, pacing.ConfirmationTimeout(response_budget), scope,
                feedback, revision, token).ConfigureAwait(false);
            if (result == BuildConfirmation.Confirmed)
            {
                pacing.Confirmed(attempt > 0);
                return;
            }
            pacing.Backoff(options.TimeoutMilliseconds);
            progress.Publish(new(result == BuildConfirmation.Throttled ? "throttled" : "retry", attempt + 1, maximum_attempts, $"{id}:{name}"));
        }
        int final_wait = options.TimeoutMilliseconds - (int)clock.GetElapsedTime(started).TotalMilliseconds;
        if (final_wait > 0 && await confirmation(matches, final_wait, scope, feedback, feedback.Throttles, token, false).ConfigureAwait(false) == BuildConfirmation.Confirmed)
            return;
        throw new TimeoutException($"{id}: {name} did not confirm value {value} within {options.TimeoutMilliseconds} ms.");
    }

    private void verify_items(WiredBuildRequest request, Dictionary<string, Id> ids,
        Dictionary<string, FurniInfo> infos, HashSet<Id> initial_items)
    {
        game.Room.Capture(room => {
            HashSet<Id> assigned = ids.Values.ToHashSet();
            var unexpected_items = room.FloorItems.Where(furni => !initial_items.Contains(furni.Id) && !assigned.Contains(furni.Id))
                .GroupBy(furni => (furni.Kind, furni.X, furni.Y)).ToDictionary(group => group.Key, group => group.Select(furni => furni.Id).ToArray());
            foreach (WiredBuildItem item in request.Items)
            {
                FloorItem? actual = room.FloorItem(ids[item.Key]);
                if (actual is null || actual.Kind != infos[item.Key].Kind || actual.X != item.X || actual.Y != item.Y || actual.Direction != item.Direction)
                    throw new InvalidOperationException($"{item.Key}: confirmed furniture was removed or changed during construction.");
                if (item.State is { } state && actual.State != state ||
                    item.AltitudeCentimeters is { } altitude && Math.Abs(actual.Z - altitude / 100.0) >= 0.004)
                    throw new InvalidOperationException($"{item.Key}: the final state or altitude no longer matches the plan.");
                if (unexpected_items.TryGetValue((actual.Kind, item.X, item.Y), out Id[]? unexpected))
                    throw new InvalidOperationException($"{item.Key}: unexpected late placement IDs {string.Join(", ", unexpected)}; inspect before retrying.");
            }
            return true;
        });
    }

    private FurniInfo definition(WiredBuildItem item)
    {
        FurniInfo info = game.GameData.Furni?.GetInfo(item.ClassName)
            ?? throw new InvalidOperationException($"{item.Key}: unknown class '{item.ClassName}'; furniture data must be loaded.");
        if (info.Type != ItemType.Floor || !string.Equals(info.Identifier, item.ClassName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{item.Key}: expected an exact floor class identifier, including any color suffix.");
        return info;
    }

    private FloorItem? find(WiredBuildItem item, int kind, HashSet<Id> claimed) => game.Room.Capture(room =>
    {
        FloorItem[] matches = room.FloorItems.Where(furni => item.ExistingId is { } id ? furni.Id == id :
            item.ReuseAtPosition && furni.Kind == kind && furni.X == item.X && furni.Y == item.Y && !claimed.Contains(furni.Id)).ToArray();
        if (matches.Length > 1) throw new InvalidOperationException($"{item.Key}: ambiguous furniture at ({item.X},{item.Y}); supply ExistingId.");
        FloorItem? match = matches.SingleOrDefault();
        if (item.ExistingId is not null && match is null) throw new InvalidOperationException($"{item.Key}: the explicit existing item is missing.");
        if (match is not null && (match.Kind != kind || match.X != item.X || match.Y != item.Y || match.Direction != item.Direction || claimed.Contains(match.Id)))
            throw new InvalidOperationException($"{item.Key}: existing furniture has a different class, position, direction or is already assigned.");
        return match;
    });

    private (int Page, int Offer)? offer(int kind)
    {
        foreach (var page in game.Catalog.CachedPages("BUILDERS_CLUB"))
            foreach (var candidate in page.Offers)
                if (candidate.Products.Count == 1 && candidate.Products[0].ProductType == "s" && candidate.Products[0].FurniClassId == kind)
                    return (page.PageId, candidate.OfferId);
        return null;
    }

    private async Task<Id> place(WiredBuildItem item, int kind, (int Page, int Offer) route, long catalog_generation,
        WiredBuildOptions options, Scope scope, WiredBuildFeedback feedback, CancellationToken cancellation)
    {
        HashSet<Id> before = game.Room.Capture(room => room.FloorItems.Select(furni => furni.Id).ToHashSet());
        Id? received()
        {
            FloorItem[] matches = game.Room.Capture(room => room.FloorItems.Where(furni => !before.Contains(furni.Id) && furni.Kind == kind &&
                furni.X == item.X && furni.Y == item.Y).ToArray());
            if (matches.Length > 1) throw new InvalidOperationException($"{item.Key}: multiple placement results arrived; inspect these items before retrying.");
            if (matches.Length == 0) return null;
            return matches[0].Id;
        }
        if (!placement_pacing.TryGetValue(kind, out WiredBuildPacing? pacing))
            placement_pacing.Add(kind, pacing = new(clock));
        feedback.Placement(route.Page, route.Offer, item.X, item.Y);
        for (int attempt = 0; attempt < options.PlacementAttempts; attempt++)
        {
            current(scope);
            feedback.Check();
            if (received() is { } existing) return existing;
            long revision = feedback.Throttles;
            int pause = (int)Math.Ceiling(pacing.RemainingSpacing(options.PlacementSpacingMilliseconds).TotalMilliseconds);
            if (pause > 0)
            {
                await confirmation(() => received() is not null, pause, scope, feedback, revision, cancellation, false).ConfigureAwait(false);
                if (received() is { } late) return late;
            }
            CatalogStateView catalog = await call<CatalogStateRequest, CatalogStateView>(ApplicationMemberIds.CatalogState,
                new("BUILDERS_CLUB"), cancellation).ConfigureAwait(false);
            current(scope);
            if (catalog.CatalogGeneration != catalog_generation) throw new InvalidOperationException("The catalog changed during construction. Recheck the plan.");
            if (received() is { } reconciled) return reconciled;
            pacing.Sent();
            await call<SubscriptionBuildersClubFloorPlaceRequest, SubscriptionBuildersClubPlacementDispatchReceipt>(
                ApplicationMemberIds.SubscriptionsBuildersClubFloorOfferPlace,
                new(route.Page, route.Offer, item.X, item.Y, item.Direction, IsRetry: true, ExpectedRoomGeneration: scope.Generation), cancellation).ConfigureAwait(false);
            BuildConfirmation result = await confirmation(() => received() is not null, pacing.ConfirmationTimeout(options.TimeoutMilliseconds),
                scope, feedback, revision, cancellation).ConfigureAwait(false);
            if (result == BuildConfirmation.Confirmed)
            {
                pacing.Confirmed(attempt > 0);
                return received()!.Value;
            }
            pacing.Backoff(options.TimeoutMilliseconds);
            progress.Publish(new(result == BuildConfirmation.Throttled ? "throttled" : "retry", attempt + 1, options.PlacementAttempts, item.Key));
        }
        await confirmation(() => received() is not null, options.TimeoutMilliseconds, scope, feedback, feedback.Throttles, cancellation, false).ConfigureAwait(false);
        if (received() is { } last) return last;
        throw new TimeoutException($"{item.Key}: placement was not observed. Earlier sends may still arrive; inspect before restarting.");
    }

    private enum BuildConfirmation { Confirmed, Throttled, Timeout }

    private async Task<BuildConfirmation> confirmation(Func<bool> predicate, int timeout, Scope scope,
        WiredBuildFeedback feedback, long revision, CancellationToken cancellation, bool stop_on_throttle = true)
    {
        long started = clock.GetTimestamp();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            current(scope);
            Task changed = feedback.Changed;
            feedback.Check();
            if (predicate()) return BuildConfirmation.Confirmed;
            if (stop_on_throttle && feedback.Throttles != revision) return BuildConfirmation.Throttled;
            double remaining = timeout - clock.GetElapsedTime(started).TotalMilliseconds;
            if (remaining <= 0) return BuildConfirmation.Timeout;
            try { await changed.WaitAsync(TimeSpan.FromMilliseconds(remaining), clock, cancellation).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                current(scope);
                feedback.Check();
                return predicate() ? BuildConfirmation.Confirmed : BuildConfirmation.Timeout;
            }
        }
    }

    private async Task health(WiredBuildPreflight baseline, WiredBuildOptions options, Scope scope, CancellationToken token)
    {
        current(scope);
        WiredRoomStats stats = await room_stats(scope, options.TimeoutMilliseconds, token).ConfigureAwait(false);
        current(scope);
        if (!double.IsFinite(stats.RoomStats.ExecutionCost) || !double.IsFinite(stats.RoomStats.ExecutionCostCap) ||
            stats.RoomStats.ExecutionCostCap <= 0 || stats.RoomStats.ExecutionCost >= stats.RoomStats.ExecutionCostCap * options.MaximumUsageFraction)
            throw new InvalidOperationException("Construction stopped because the room exhausted the configured usage headroom.");
        WiredErrorLogs errors = await room_read<WiredErrorLogs>(ApplicationMemberIds.WiredRoomErrorLogsGet,
            scope, options.TimeoutMilliseconds, token).ConfigureAwait(false);
        current(scope);
        string[] added = errors.Errors.Where(error => error.ThrowCount >
            (baseline.Errors.FirstOrDefault(previous => previous.ErrorName == error.ErrorName)?.ThrowCount ?? 0)).Select(error => error.ErrorName).ToArray();
        if (added.Length > 0) throw new InvalidOperationException("Construction stopped after new Wired errors: " + string.Join(", ", added));
    }

    private Task<WiredRoomStats> room_stats(Scope scope, int timeout, CancellationToken token) =>
        room_read<WiredRoomStats>(ApplicationMemberIds.WiredRoomStatsGet, scope, timeout, token);

    private async Task<T> room_read<T>(string operation, Scope scope, int timeout, CancellationToken token)
    {
        long started = clock.GetTimestamp();
        for (int attempt = 0; ; attempt++)
        {
            current(scope);
            int remaining = Math.Max(1, timeout - (int)clock.GetElapsedTime(started).TotalMilliseconds);
            int window = attempt < 3 ? Math.Min(remaining, 500 << attempt) : remaining;
            try
            {
                var result = await call<WiredTimeoutRequest, T>(operation,
                    new(window), token).ConfigureAwait(false);
                current(scope);
                return result;
            }
            catch (RequestTimeoutException) when (attempt < 3 && clock.GetElapsedTime(started).TotalMilliseconds < timeout)
            {
                current(scope);
            }
        }
    }

    private Scope capture()
    {
        Session session = interceptor.Session ?? throw new InvalidOperationException("No hotel session is connected.");
        return game.Room.Capture(room => room.IsReady ? new Scope(session, room.RoomId, room.Generation)
            : throw new InvalidOperationException("A ready room is required."));
    }

    private void current(Scope scope)
    {
        if (!ReferenceEquals(scope.Session, interceptor.Session) || !game.Room.Capture(room => room.IsReady && room.RoomId == scope.RoomId && room.Generation == scope.Generation))
            throw new InvalidOperationException("The session or room changed. Construction stopped without touching the new room.");
    }

    private static bool empty(WiredFormPatch patch) => (patch.Fields?.Count ?? 0) == 0 && patch.Furniture is null &&
        patch.SecondaryFurniture is null && (patch.FurnitureSources?.Count ?? 0) == 0 && (patch.UserSources?.Count ?? 0) == 0 &&
        patch.ActionDelay is null && patch.ConditionQuantifier is null && patch.SelectorFilter is null && patch.SelectorInvert is null;

    private static WiredBuildRequest snapshot(WiredBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Items);
        return request with { Items = request.Items.Select(item => item is null ? null! : item with {
            Settings = item.Settings is null ? null : snapshot(item.Settings),
            FurnitureKeys = item.FurnitureKeys?.ToArray(), SecondaryFurnitureKeys = item.SecondaryFurnitureKeys?.ToArray(),
            VariablesByName = item.VariablesByName?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
        }).ToArray() };
    }

    private static WiredFormPatch snapshot(WiredFormPatch patch) => patch with {
        Fields = patch.Fields?.Select(edit => edit with { Value = edit.Value with {
            Tiles = edit.Value.Tiles?.ToArray(), Rewards = edit.Value.Rewards?.ToArray()
        } }).ToArray(), Furniture = patch.Furniture?.ToArray(), SecondaryFurniture = patch.SecondaryFurniture?.ToArray(),
        FurnitureSources = patch.FurnitureSources?.ToArray(), UserSources = patch.UserSources?.ToArray()
    };

    private sealed record Scope(Session Session, long RoomId, long Generation);

    private sealed class DialogScope : IDisposable
    {
        private readonly List<IDisposable> hooks = [];
        public DialogScope(IInterceptor interceptor, bool hide)
        {
            if (!hide) return;
            try
            {
                MessageKey[] keys = [MessageKeys.Wired.Configuration.Opened, MessageKeys.Wired.Configuration.Trigger,
                    MessageKeys.Wired.Configuration.Action, MessageKeys.Wired.Configuration.Condition,
                    MessageKeys.Wired.Configuration.Selector, MessageKeys.Wired.Configuration.Addon, MessageKeys.Wired.Configuration.Variable];
                foreach (MessageKey key in keys) hooks.Add(interceptor.Intercept(key, message => message.Block()));
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            foreach (IDisposable hook in hooks) hook.Dispose();
            hooks.Clear();
        }
    }
}
