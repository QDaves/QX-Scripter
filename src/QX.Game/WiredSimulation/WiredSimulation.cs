using Qx.Model;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private readonly WiredSimulationOptions options;
    private readonly Dictionary<long, WiredSimulationFurniture> furniture;
    private readonly Dictionary<long, WiredSimulationUser> users;
    private readonly Dictionary<string, WiredSimulationVariable> variables;
    private readonly Dictionary<string, WiredSimulationValue> globals;
    private readonly Dictionary<long, WiredSimulationNode> nodes;
    private readonly bool has_fx;
    private readonly PriorityQueue<Work, (long Time, long Sequence)> queue = new();
    private readonly List<WiredSimulationDiagnostic> diagnostics = [];
    private readonly List<WiredSimulationTrace> trace = [];
    private readonly List<WiredSimulationEffect> effects = [];
    private readonly Dictionary<(long Root, long Stack), int> visits = [];
    private readonly Dictionary<long, long> root_work = [];
    private readonly Dictionary<long, int> root_moves = [];
    private readonly Dictionary<long, long> publication_roots = [];
    private readonly Dictionary<long, HashSet<long>> unseen = [];
    private readonly Dictionary<long, StateChange> state_changes = [];
    private readonly Dictionary<int, long> team_scores = [];
    private readonly WiredSimulationRoom initial;
    private long now, sequence, root_sequence, next_temporary = -1, events, activations, actions, work_units;
    private long reads, writes, candidates, moves, placements, peak_pending, peak_work;
    private uint random;
    private WiredSimulationStatus status = WiredSimulationStatus.Idle;
    private bool stopped, trace_truncated, started;
    private long trace_sequence;
    private long immediate_sequence;
    private long bot_revision;
    private long transaction_revision;
    private int bot_choice;
    private int bot_delivery_choice;
    private int bot_movement_choice;
    private int furniture_transfer_choice;
    private int spawn_value_choice;
    private int carrier_contact_choice;
    private int? bot_phase;
    private int? bot_teleport_phase;
    private int? bot_follow_phase;
    private int? bot_delivery_phase;
    private int? projectile_phase;
    private int reward_outcome_index;
    private long projectile_revision;
    private readonly Dictionary<long, ProjectileFlight> projectiles = [];

    private sealed record Frame(long Root, WiredSimulationEvent Event,
        Dictionary<string, long> Context, long[] SelectedFurniture, long[] SelectedUsers,
        List<long> Moved, long? Stack = null, Dictionary<long, Selection>? Selections = null, bool InlineActions = false,
        WiredSimulationUser? DepartedUser = null, bool GameNotification = false,
        Dictionary<string, string>? TextValues = null, long? Action = null);

    private sealed record Selection(long[] Furniture, long[] Users);

    private sealed record StateChange(long At, long PublicationRoot, int PreviousState);

    private sealed record Teleport(long UserId, long TargetId, int TargetX, int TargetY, int PreviousEffect, bool Restore = false,
        bool Bot = false, double TargetTop = 0, AvatarStance TargetStance = AvatarStance.Stand);

    private sealed record BotWalk(long UserId, long TargetId, int TargetX, int TargetY, int StepMilliseconds,
        long ActionId, double TargetTop, IReadOnlyList<Tile> Path, int Step = 1, long FollowRevision = 0, int StepsTaken = 0,
        long DeliveryRevision = 0, IReadOnlySet<long>? BotGroup = null, AvatarStance TargetStance = AvatarStance.Stand,
        double TargetBase = 0);

    private sealed record BotArrival(long BotId, long Revision);
    private sealed record BotHandoff(long BotId, long Revision);

    private sealed record ProjectileStep(long FurnitureId, long Revision);
    private sealed record TradeTimeout(long UserId, long Revision, long ActionId);
    private sealed record RoomDeparture(long UserId, long ActionId, long SourceId, WiredSimulationRoomLink Destination);

    private sealed record Work(long At, long Sequence, Frame Frame,
        long? Action = null, long? Trigger = null, bool External = false, bool ClockTick = false, bool TimerReset = false,
        Teleport? Teleport = null, BotWalk? BotWalk = null, BotArrival? BotArrival = null, BotHandoff? BotHandoff = null,
        ProjectileStep? Projectile = null, TradeTimeout? TradeTimeout = null, RoomDeparture? RoomDeparture = null);

    public WiredSimulation(WiredSimulationRoom room, WiredSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(room);
        this.options = options is null ? new() : options with
        {
            BotFollowChoices = options.BotFollowChoices?.ToArray(),
            BotDeliveryChoices = options.BotDeliveryChoices?.ToArray(),
            BotMovementChoices = copy_bot_movement_choices(options.BotMovementChoices),
            FurnitureTransferChoices = copy_transfer_choices(options.FurnitureTransferChoices),
            SpawnValueChoices = options.SpawnValueChoices?.ToArray(),
            RandomChoices = copy_random_choices(options.RandomChoices),
            CarrierContactChoices = options.CarrierContactChoices?.ToArray(),
            RewardOutcomes = options.RewardOutcomes?.ToArray()
        };
        if (this.options.MaximumEvents <= 0 || this.options.MaximumWork <= 0 ||
            this.options.MaximumTimeMilliseconds <= 0 || this.options.MaximumTrace < 0 ||
            this.options.MaximumFurniture <= 0 || this.options.MaximumPendingEvents <= 0 ||
            this.options.MaximumMovesPerExecution <= 0 || this.options.MaximumStackVisitsPerExecution <= 0 ||
            this.options.PulseMilliseconds <= 0 || this.options.ShortPulseMilliseconds <= 0 ||
            this.options.MaximumSelectionDepth is < 1 or > 256 ||
            this.options.VariableBits is not (32 or 64) ||
            this.options.BotFollowChoices?.Any(choice => choice is null) == true ||
            this.options.BotFollowChoices?.Count > this.options.MaximumEvents ||
            this.options.BotDeliveryChoices?.Any(choice => choice is null) == true ||
            this.options.BotDeliveryChoices?.Count > this.options.MaximumEvents ||
            this.options.BotMovementChoices?.Count > this.options.MaximumEvents ||
            this.options.BotMovementChoices?.Any(choice => choice.Steps.Count > 10000) == true ||
            this.options.FurnitureTransferChoices?.Count > this.options.MaximumEvents ||
            this.options.FurnitureTransferChoices?.Any(choice => choice.Steps.Count > this.options.MaximumFurniture) == true ||
            this.options.SpawnValueChoices?.Count > this.options.MaximumEvents ||
            this.options.SpawnValueChoices?.Any(choice => choice is null || choice.Domain is not (WiredSourceDomain.Furniture or WiredSourceDomain.User)) == true ||
            this.options.RandomChoices?.Count > this.options.MaximumEvents || this.options.RandomChoices?.Any(choice => choice.ActionIds.Count > 100) == true ||
            this.options.CarrierContactChoices?.Count > this.options.MaximumEvents || this.options.CarrierContactChoices?.Any(choice => choice is null) == true ||
            this.options.RewardOutcomes?.Count > this.options.MaximumEvents ||
            this.options.RewardOutcomes?.Any(outcome => outcome is null || outcome.ActionId == 0 || outcome.UserId == 0 || outcome.RewardIndex < 0) == true)
            throw new ArgumentException("Invalid simulation limits or choices.", nameof(options));
        if (room.Width <= 0 || room.Height <= 0 || (long)room.Width * room.Height > 1000000 ||
            room.Floor is { } floor && floor.Count != (long)room.Width * room.Height)
            throw new ArgumentException("Invalid room floor dimensions.", nameof(room));
        if (room.Furniture.Count > this.options.MaximumFurniture || room.Users.Count > 10000)
            throw new ArgumentException("The snapshot exceeds simulation capacity.", nameof(room));
        initial = room with { Floor = room.Floor?.ToArray(), Incomplete = room.Incomplete?.ToArray(),
            FxPresentation = copy_fx(room.FxPresentation), TeamOrder = room.TeamOrder?.ToArray(), Game = copy_game(room.Game),
            Api = room.Api is { } api ? api with { Addresses = api.Addresses?.ToArray() } : null,
            Visuals = room.Visuals?.ToArray(), Links = room.Links?.ToArray(), SharedPlaceholders = room.SharedPlaceholders?.ToArray(),
            RandomHistory = copy_random_history(room.RandomHistory), LimitHistory = copy_limit_history(room.LimitHistory) };
        clock_phase = room.ClockTickOffsetMilliseconds;
        bot_phase = room.BotTiming?.NextStepOffsetMilliseconds;
        bot_teleport_phase = room.BotTiming?.TeleportClock?.NextTickOffsetMilliseconds;
        bot_follow_phase = room.BotTiming?.FollowStartOffsetMilliseconds;
        bot_delivery_phase = room.BotTiming?.DeliveryStartOffsetMilliseconds;
        projectile_phase = room.ProjectileTiming?.NextTickOffsetMilliseconds;
        restore_timer(room.Timer);
        furniture = room.Furniture.ToDictionary(item => item.Id, copy);
        long minimum_id = Math.Min(0, furniture.Keys.DefaultIfEmpty(0).Min());
        if (minimum_id == long.MinValue) throw new ArgumentException("Furniture IDs leave no space for temporary IDs.", nameof(room));
        next_temporary = minimum_id - 1;
        users = room.Users.ToDictionary(user => user.Id, copy);
        transaction_revision = users.Values.Select(user => user.Trade?.Revision ?? 0).DefaultIfEmpty().Max();
        bot_revision = users.Values.Select(user => Math.Max(user.Follow?.Revision ?? 0, user.Delivery?.Revision ?? 0)).DefaultIfEmpty().Max();
        variables = (room.Variables ?? []).ToDictionary(variable => variable.Id,
            variable => variable with { Text = variable.Text?.ToDictionary(), ObservedHolders = variable.ObservedHolders?.ToArray() }, StringComparer.Ordinal);
        globals = (room.GlobalVariables ?? new Dictionary<string, WiredSimulationValue>()).ToDictionary(StringComparer.Ordinal);
        nodes = room.Nodes.ToDictionary(node => node.FurnitureId, copy);
        restore_random_history();
        restore_limit_history();
        has_fx = nodes.Values.Any(node => node.Category == WiredFormCategory.Addon && node.Code is >= 1200 and <= 1205);
        foreach (var pair in room.TeamScores ?? new Dictionary<int, long>()) team_scores.Add(pair.Key, pair.Value);
        restore_team_order(room.TeamOrder);
        restore_game();
        restore_stores();
        random = unchecked((uint)this.options.Seed) | 1;
        validate();
        if (status != WiredSimulationStatus.Faulted) restore_visuals();
        if (status != WiredSimulationStatus.Faulted) restore_projectiles();
    }

    public long Time => now;
    public WiredSimulationStatus Status => status;

    public WiredSimulationResult Inspect()
    {
        var displays = variable_fx();
        return new(status, now, snapshot(),
            new(events, activations, actions, work_units, reads, writes, candidates, moves, placements, peak_pending, peak_work),
            diagnostics.ToArray(), trace.Select(value => value with { Targets = value.Targets?.ToArray() }).ToArray(),
            effects.Select(copy_effect).ToArray(), queue.Count, trace_truncated,
            ApiResponses: api_responses.Select(copy).ToArray(), VariableFx: displays, Visuals: visual_samples());
    }

    public WiredSimulation Fork()
    {
        if (!fx_initialized || !fx_pending()) refresh_fx();
        var clone = new WiredSimulation(snapshot(), options)
        {
            now = now, sequence = sequence, root_sequence = root_sequence, next_temporary = next_temporary,
            events = events, activations = activations, actions = actions, work_units = work_units,
            reads = reads, writes = writes, candidates = candidates, moves = moves, placements = placements,
            peak_pending = peak_pending, peak_work = peak_work, random = random, status = status, stopped = stopped,
            trace_truncated = trace_truncated, trace_sequence = trace_sequence, immediate_sequence = immediate_sequence, started = started,
            bot_revision = bot_revision, bot_choice = bot_choice, bot_phase = bot_phase, bot_teleport_phase = bot_teleport_phase, bot_follow_phase = bot_follow_phase,
            bot_delivery_phase = bot_delivery_phase, bot_delivery_choice = bot_delivery_choice,
            bot_movement_choice = bot_movement_choice,
            furniture_transfer_choice = furniture_transfer_choice,
            spawn_value_choice = spawn_value_choice, random_choice = random_choice, carrier_contact_choice = carrier_contact_choice,
            transaction_revision = transaction_revision, reward_outcome_index = reward_outcome_index
        };
        clone.diagnostics.Clear(); clone.diagnostics.AddRange(diagnostics);
        clone.trace.AddRange(trace.Select(value => value with { Targets = value.Targets?.ToArray() }));
        clone.effects.AddRange(effects.Select(copy_effect));
        foreach (var pair in users) clone.users[pair.Key] = clone.users[pair.Key] with { Activity = pair.Value.Activity };
        clone.api_responses.AddRange(api_responses.Select(copy));
        clone.visuals.Clear();
        foreach (var pair in visuals) clone.visuals.Add(pair.Key, pair.Value);
        clone.clock_phase = clock_phase;
        clone.timer_origin = timer_origin;
        clone.timer_reset_at = timer_reset_at;
        clone.timer_reset_queued = timer_reset_queued;
        clone.timers_initialized = timers_initialized;
        clone.clock_tick_pending = clock_tick_pending;
        clone.fx_memory.Clear();
        foreach (var pair in fx_memory) clone.fx_memory.Add(pair.Key, pair.Value);
        foreach (var pair in fx_hover) clone.fx_hover.Add(pair.Key, pair.Value);
        clone.fx_changes.UnionWith(fx_changes);
        clone.fx_initialized = fx_initialized;
        clone.fx_current = fx_current.Select(copy_fx_display).ToArray();
        clone.projectile_phase = projectile_phase;
        clone.projectile_revision = projectile_revision;
        clone.projectiles.Clear();
        foreach (var pair in projectiles) clone.projectiles.Add(pair.Key, pair.Value with { Path = pair.Value.Path.ToArray() });
        foreach (var pair in visits) clone.visits.Add(pair.Key, pair.Value);
        foreach (var pair in root_work) clone.root_work.Add(pair.Key, pair.Value);
        foreach (var pair in root_moves) clone.root_moves.Add(pair.Key, pair.Value);
        foreach (var pair in publication_roots) clone.publication_roots.Add(pair.Key, pair.Value);
        clone.limited.Clear();
        foreach (var pair in limited) clone.limited.Add(pair.Key, pair.Value with
            { Activations = new(pair.Value.Activations), ConditionFailures = new(pair.Value.ConditionFailures) });
        foreach (var pair in unseen) clone.unseen.Add(pair.Key, [.. pair.Value]);
        foreach (var pair in state_changes) clone.state_changes.Add(pair.Key, pair.Value);
        var frames = new Dictionary<Frame, Frame>(ReferenceEqualityComparer.Instance);
        var contexts = new Dictionary<Dictionary<string, long>, Dictionary<string, long>>(ReferenceEqualityComparer.Instance);
        var moved = new Dictionary<List<long>, List<long>>(ReferenceEqualityComparer.Instance);
        foreach (var entry in queue.UnorderedItems)
        {
            var source = entry.Element.Frame;
            if (!frames.TryGetValue(source, out var frame))
            {
                if (!contexts.TryGetValue(source.Context, out var context)) contexts.Add(source.Context, context = source.Context.ToDictionary(StringComparer.Ordinal));
                if (!moved.TryGetValue(source.Moved, out var items)) moved.Add(source.Moved, items = [.. source.Moved]);
                frames.Add(source, frame = copy(source) with { Context = context, Moved = items });
            }
            clone.queue.Enqueue(entry.Element with { Frame = frame }, entry.Priority);
        }
        return clone;
    }

    public void Stop() { stopped = true; status = WiredSimulationStatus.Stopped; }

    public WiredSimulationResult Run(IEnumerable<WiredSimulationEvent>? input = null,
        long advance_milliseconds = 60000, int? maximum_events = null,
        IReadOnlySet<long>? breakpoints = null, CancellationToken cancellation = default)
    {
        if (status == WiredSimulationStatus.Faulted || stopped) return Inspect();
        cancellation.ThrowIfCancellationRequested();
        if (advance_milliseconds < 0 || advance_milliseconds > options.MaximumTimeMilliseconds - now)
            throw new ArgumentOutOfRangeException(nameof(advance_milliseconds));
        int budget = maximum_events ?? options.MaximumEvents;
        if (budget <= 0 || budget > options.MaximumEvents) throw new ArgumentOutOfRangeException(nameof(maximum_events));
        var submitted = new List<WiredSimulationEvent>();
        foreach (var stimulus in input ?? [])
        {
            cancellation.ThrowIfCancellationRequested();
            if (stimulus is null || !Enum.IsDefined(stimulus.Kind)) throw new ArgumentException("Unknown simulation event kind.", nameof(input));
            if ((stimulus.Kind == WiredSimulationEventKind.Hover) != (stimulus.Hover is not null))
                throw new ArgumentException("Only hover events require an FX hover payload.", nameof(input));
            if ((stimulus.Kind == WiredSimulationEventKind.VariableObserved) != (stimulus.Observation is not null))
                throw new ArgumentException("VariableObserved events require a variable observation exclusively.", nameof(input));
            if ((stimulus.Kind is WiredSimulationEventKind.LinkMessage or WiredSimulationEventKind.LinkDataframe) != (stimulus.Link is not null))
                throw new ArgumentException("Link events require a Link payload exclusively.", nameof(input));
            if ((stimulus.Kind is WiredSimulationEventKind.OfferTrade or WiredSimulationEventKind.AcceptTrade or WiredSimulationEventKind.ConfirmTrade) != (stimulus.Trade is not null))
                throw new ArgumentException("Trade offer, acceptance and confirmation events require a trade payload exclusively.", nameof(input));
            if (stimulus.At < now || stimulus.At > options.MaximumTimeMilliseconds)
                throw new ArgumentException("An event must lie between current time and the configured time limit.", nameof(input));
            if (submitted.Count >= options.MaximumPendingEvents - queue.Count)
            { fail("simulation.pending_limit", "Too many submitted events."); return Inspect(); }
            submitted.Add(copy(stimulus));
        }
        if (!fx_initialized || !fx_pending()) refresh_fx();
        if (status == WiredSimulationStatus.Faulted) return Inspect();
        foreach (var stimulus in submitted)
        {
            var frame = new Frame(++root_sequence, copy(stimulus),
                (stimulus.Context ?? new Dictionary<string, long>()).ToDictionary(StringComparer.Ordinal), [], [], []);
            try { enqueue(stimulus.At, frame, external: true); }
            catch (SimulationFailure) { return Inspect(); }
        }
        if (status == WiredSimulationStatus.Faulted) return Inspect();
        status = WiredSimulationStatus.Running;
        try { schedule_clock(); schedule_timer_reset(); schedule_projectiles(); }
        catch (SimulationFailure) { return Inspect(); }
        long until = checked(now + advance_milliseconds);
        int steps = 0;
        while (queue.TryPeek(out var next, out var priority) && priority.Time <= until)
        {
            if (cancellation.IsCancellationRequested)
            { status = WiredSimulationStatus.Paused; cancellation.ThrowIfCancellationRequested(); }
            if (steps >= budget || breakpoints?.Contains(next.Action ?? next.Trigger ?? 0) == true)
            { status = WiredSimulationStatus.Paused; return Inspect(); }
            queue.Dequeue(); now = priority.Time;
            if (++events > options.MaximumEvents) { fail("simulation.event_limit", "The deterministic event limit was reached."); break; }
            steps++;
            try
            {
                if (next.Teleport is { } transfer) finish_teleport(transfer, next.Frame);
                else if (next.BotWalk is { } walking) bot_step(walking, next.Frame);
                else if (next.BotArrival is { } arrival) bot_arrival(arrival, next.Frame);
                else if (next.BotHandoff is { } handoff) bot_handoff(handoff, next.Frame);
                else if (next.Projectile is { } projectile) projectile_step(projectile, next.Frame);
                else if (next.TradeTimeout is { } timeout) trade_timeout(timeout);
                else if (next.RoomDeparture is { } departure) leave_through_link(departure, next.Frame);
                else if (next.TimerReset) apply_timer_reset();
                else if (next.ClockTick) clock_tick();
                else if (next.External) stimulus(next.Frame);
                else if (next.Action is { } action_id && nodes.TryGetValue(action_id, out var action)) action_run(action, next.Frame);
                else if (next.Trigger is { } trigger_id && nodes.TryGetValue(trigger_id, out var trigger))
                { stack(trigger, next.Frame); repeat(trigger, next.Frame); }
                if (!fx_pending()) refresh_fx();
            }
            catch (SimulationFailure) { }
            catch (Exception error) when (error is ArithmeticException or ArgumentException or InvalidOperationException)
            { fail("simulation.invalid_operation", error.Message, next.Action ?? next.Trigger); }
            if (status == WiredSimulationStatus.Faulted || stopped) break;
        }
        if (status == WiredSimulationStatus.Running)
        {
            status = queue.Count == 0 ? WiredSimulationStatus.Completed : WiredSimulationStatus.Paused;
            now = until;
        }
        return Inspect();
    }

    public IReadOnlyList<WiredSimulationAssertionResult> Assert(IEnumerable<WiredSimulationAssertion> assertions) =>
        assertions.Select(assertion =>
        {
            furniture.TryGetValue(assertion.EntityId ?? 0, out var item);
            users.TryGetValue(assertion.EntityId ?? 0, out var user);
            bool known = true;
            long? read(WiredSourceDomain domain, long id)
            {
                var result = assertion_variable(domain, id, assertion.Variable, !assertion.Kind.EndsWith("_present", StringComparison.Ordinal));
                known &= result.Known;
                return result.Value;
            }
            long? value = assertion.Kind switch
            {
                "state" => item?.State, "effect" => user?.Effect, "score" => user?.Score,
                "clock_milliseconds" => item?.Clock?.ElapsedMilliseconds,
                "clock_running" => item?.Clock is { } clock ? clock.Running ? 1 : 0 : null,
                "furniture_variable" or "furniture_variable_present" => item is null ? null : read(WiredSourceDomain.Furniture, item.Id),
                "user_variable" or "user_variable_present" => user is null ? null : read(WiredSourceDomain.User, user.Id),
                "global_variable" or "global_variable_present" => read(WiredSourceDomain.Global, 0),
                "furniture_count" => furniture.Count, "temporary_count" => furniture.Values.Count(value => value.Temporary),
                _ => null
            };
            bool passed = assertion.Kind switch
            {
                "furniture_present" => (item is not null) == assertion.Present,
                "user_present" => (user is not null) == assertion.Present,
                "furniture_variable_present" or "user_variable_present" or "global_variable_present" => value.HasValue == (assertion.Present ?? true),
                "position" => (item is not null || user is not null) && assertion.X.HasValue && assertion.Y.HasValue &&
                    (item?.X ?? user?.X) == assertion.X && (item?.Y ?? user?.Y) == assertion.Y,
                "no_errors" => diagnostics.All(value => value.Severity is not (WiredSimulationSeverity.Error or WiredSimulationSeverity.Unsupported)),
                _ => value.HasValue && value == assertion.Value
            };
            return new WiredSimulationAssertionResult(assertion, known && passed, !known ? "unknown" : value?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                ?? (assertion.Kind.EndsWith("_variable_present", StringComparison.Ordinal) ? "absent"
                    : assertion.Kind == "position" ? $"{item?.X ?? user?.X},{item?.Y ?? user?.Y}" : $"furniture={item is not null}; user={user is not null}"));
        }).ToArray();

    private (bool Known, long? Value) assertion_variable(WiredSourceDomain domain, long id, string? variable, bool require_exact)
    {
        if (string.IsNullOrEmpty(variable)) return (false, null);
        long previous_reads = reads;
        try { return (true, value(domain, id, variable, new(0, new(WiredSimulationEventKind.Start, now), [], [], [], []), require_exact)?.Value); }
        catch (SimulationFailure) { return (false, null); }
        finally { reads = previous_reads; }
    }

    private static WiredSimulationFurniture copy(WiredSimulationFurniture item) => surface(item with
    { Variables = item.Variables?.ToDictionary(StringComparer.Ordinal), States = item.States?.ToDictionary(), Directions = item.Directions?.ToArray(),
        Projectile = item.Projectile is { } projectile ? projectile with { FurnitureHits = projectile.FurnitureHits?.ToArray()!, UserHits = projectile.UserHits?.ToArray()! } : null,
        Chest = copy_chest(item.Chest), RecolorPalette = item.RecolorPalette?.ToArray(),
        Contract = item.Contract is { } contract ? WiredManager.SnapshotOf(contract) : null });
    private static WiredSimulationFurniture surface(WiredSimulationFurniture item) =>
        item.States?.GetValueOrDefault(item.State) is { } state
            ? item with { Walkable = state.Walkable, Height = state.Height, Stackable = state.Stackable, Stance = state.Stance }
            : item;
    private static WiredSimulationUser copy(WiredSimulationUser user) => user with
    { Variables = user.Variables?.ToDictionary(StringComparer.Ordinal), Groups = user.Groups?.ToArray(), Badges = user.Badges?.ToArray(), Trade = copy_trade(user.Trade), WiredContacts = user.WiredContacts?.ToArray(),
        Achievements = user.Achievements?.ToArray(), RewardTracks = user.RewardTracks?.Select(track => track?.Tasks is null
            ? throw new ArgumentException("Reward tracks require an explicit task list.") : track with { Tasks = track.Tasks.ToArray() }).ToArray() };
    private static WiredSimulationNode copy(WiredSimulationNode node) => node with
    {
        Fields = WiredSimulationNode.copy_fields(node.Category, node.Code, node.Fields.Select(pair => KeyValuePair.Create(pair.Key,
            pair.Value with { Tiles = pair.Value.Tiles?.ToArray(), Rewards = pair.Value.Rewards?.ToArray(), SendRows = pair.Value.SendRows?.ToArray(), ReceiveRows = pair.Value.ReceiveRows?.ToArray() }))),
        Furniture = node.Furniture?.ToArray(), SecondaryFurniture = node.SecondaryFurniture?.ToArray(),
        FurnitureSources = node.FurnitureSources?.ToArray(), UserSources = node.UserSources?.ToArray(),
        SavedFurniture = node.SavedFurniture?.Select(copy).ToArray(),
        SavedSnapshot = node.SavedSnapshot?.ToArray(),
        MovementDirections = node.MovementDirections?.ToDictionary(), InputSources = copy_sources(node.InputSources)
    };
    private static WiredSimulationEvent copy(WiredSimulationEvent input) => input with
    { Furniture = input.Furniture?.ToArray(), Users = input.Users?.ToArray(), Context = input.Context?.ToDictionary(StringComparer.Ordinal),
        Api = input.Api is { } request ? copy(request) : null, Link = input.Link is { } link ? copy(link) : null };
    internal static WiredSimulationRoom CopyRoom(WiredSimulationRoom room) => room with
    {
        Furniture = room.Furniture.Select(copy).ToArray(), Users = room.Users.Select(copy).ToArray(), Nodes = room.Nodes.Select(copy).ToArray(),
        Variables = room.Variables?.Select(value => value with { Text = value.Text?.ToDictionary(), ObservedHolders = value.ObservedHolders?.ToArray() }).ToArray(),
        GlobalVariables = room.GlobalVariables?.ToDictionary(StringComparer.Ordinal), Floor = room.Floor?.ToArray(), Incomplete = room.Incomplete?.ToArray(),
        FxPresentation = copy_fx(room.FxPresentation), TeamOrder = room.TeamOrder?.ToArray(), Game = copy_game(room.Game), StoredVariables = copy_stores(room.StoredVariables),
        TeamScores = room.TeamScores?.ToDictionary(), Api = room.Api is { } api ? api with { Addresses = api.Addresses?.ToArray() } : null,
        Visuals = room.Visuals?.ToArray(), Links = room.Links?.ToArray(), SharedPlaceholders = room.SharedPlaceholders?.ToArray(),
        RandomHistory = copy_random_history(room.RandomHistory), LimitHistory = copy_limit_history(room.LimitHistory)
    };
    private static Frame copy(Frame frame) => frame with
    {
        Event = copy(frame.Event), Context = frame.Context.ToDictionary(StringComparer.Ordinal),
        TextValues = frame.TextValues?.ToDictionary(StringComparer.Ordinal),
        SelectedFurniture = [.. frame.SelectedFurniture], SelectedUsers = [.. frame.SelectedUsers], Moved = [.. frame.Moved],
        DepartedUser = frame.DepartedUser is { } departed ? copy(departed) : null,
        Selections = frame.Selections?.ToDictionary(pair => pair.Key, pair => new Selection([.. pair.Value.Furniture], [.. pair.Value.Users]))
    };
    private WiredSimulationRoom snapshot() => initial with
    { Furniture = furniture.Values.Select(projectile_snapshot).ToArray(), Users = users.Values.Select(activity_snapshot).ToArray(), Nodes = nodes.Values.Select(copy).ToArray(),
        Variables = variables.Values.Select(value => value with { Text = value.Text?.ToDictionary(), ObservedHolders = value.ObservedHolders?.ToArray() }).ToArray(),
        GlobalVariables = globals.ToDictionary(StringComparer.Ordinal), Floor = initial.Floor?.ToArray(), Incomplete = initial.Incomplete?.ToArray(), TeamScores = team_scores.ToDictionary(),
        Api = initial.Api is { } api ? api with { Addresses = api.Addresses?.ToArray() } : null,
        Links = initial.Links?.ToArray(), SharedPlaceholders = initial.SharedPlaceholders?.ToArray(),
        FxPresentation = fx_snapshot(), TeamOrder = team_order?.ToArray(), Game = game_snapshot(), StoredVariables = store_snapshot(), Visuals = visual_snapshot(),
        ProjectileTiming = initial.ProjectileTiming is { TickMilliseconds: > 0 } projectile_timing ? projectile_timing with
        { NextTickOffsetMilliseconds = (int)((projectile_phase!.Value - now % projectile_timing.TickMilliseconds + projectile_timing.TickMilliseconds) % projectile_timing.TickMilliseconds) } : null,
        ClockTickOffsetMilliseconds = clock_phase is { } phase ? (int)((phase - now % 500 + 500) % 500) : null,
        RandomHistory = copy_random_history(random_histories.Values.OrderBy(history => history.AddonId).ToArray()),
        LimitHistory = limit_history(),
        Timer = timer_snapshot(), BotTiming = initial.BotTiming is { } timing ? timing with
        {
            NextStepOffsetMilliseconds = bot_step_delay(),
            FollowStartOffsetMilliseconds = bot_follow_phase.HasValue ? bot_follow_delay() : null,
            DeliveryStartOffsetMilliseconds = bot_delivery_phase.HasValue ? bot_delivery_delay() : null,
            TeleportClock = timing.TeleportClock is { TickMilliseconds: > 0 } teleport_clock ? teleport_clock with
            { NextTickOffsetMilliseconds = (int)((bot_teleport_phase!.Value - now % teleport_clock.TickMilliseconds + teleport_clock.TickMilliseconds) % teleport_clock.TickMilliseconds) } : timing.TeleportClock
        } : null };

    private void validate()
    {
        validate_shared_placeholders();
        validate_pointers();
        validate_links();
        validate_projectiles();
        validate_random_history();
        validate_limit_history();
        if (initial.BotTiming is { } bot_timing && (bot_timing.StepMilliseconds <= 0 || bot_timing.StepMilliseconds > int.MaxValue / 2 ||
            bot_timing.NextStepOffsetMilliseconds < 0 || bot_timing.NextStepOffsetMilliseconds >= bot_timing.StepMilliseconds ||
            bot_timing.FollowRefreshMilliseconds < 0 || bot_timing.FollowArrivalMilliseconds < 0 || bot_timing.FollowRetryMilliseconds < 0 ||
            bot_timing.FollowStartOffsetMilliseconds is { } follow_phase && (follow_phase < 0 || follow_phase >= bot_timing.StepMilliseconds * 2L) ||
            bot_timing.DeliveryStartOffsetMilliseconds is { } delivery_phase && (delivery_phase < 0 || delivery_phase >= bot_timing.StepMilliseconds * 2L) ||
            bot_timing.DeliveryArrivalMilliseconds is < 0))
            fail("snapshot.bot_clock", "Invalid bot movement clock profile.");
        if (initial.BotTiming?.TeleportClock is { } teleport_clock && (teleport_clock.TickMilliseconds <= 0 ||
            teleport_clock.NextTickOffsetMilliseconds < 0 || teleport_clock.NextTickOffsetMilliseconds >= teleport_clock.TickMilliseconds ||
            initial.BotTiming.Teleport is not { } teleport || teleport.ArrivalMilliseconds % teleport_clock.TickMilliseconds != 0 ||
            teleport.EffectMilliseconds % teleport_clock.TickMilliseconds != 0))
            fail("snapshot.bot_teleport_clock", "Invalid bot teleport clock or nonintegral tick durations.");
        if (clock_phase is < 0 or >= 500)
            fail("snapshot.clock_phase", "Clock tick offset must be within 0..499 milliseconds.");
        if (initial.Floor?.Any(height => !double.IsFinite(height)) == true)
            fail("snapshot.floor", "Floor heights must be finite numbers.");
        if (nodes.Values.Count(node => node.Name == "VariablesWebApi") > 1)
            fail("api.configuration", "Multiple Web API add-ons have unverified precedence; use one configuration per room snapshot.");
        foreach (var item in furniture.Values)
        {
            if (item.IntrinsicType is < 0 or > 2 || item.IntrinsicType is { } type && (type == 2) != item.Temporary)
                fail("snapshot.furniture_type", $"Furniture type and temporary status disagree: {item.Id}.", item.Id);
            if (item.RecolorRgb is < 0 or > 0xffffff || item.RecolorPalette?.Any(color => color is < 0 or > 0xffffff) == true)
                fail("snapshot.recolor", $"Invalid furniture color or palette: {item.Id}.", item.Id);
            if (item.Chest is { } chest && (chest.Coins is < 0 || chest.Capacity is < 0 || chest.Items?.Any(entry => entry is null || entry.Type is null || entry.Data is null) == true ||
                chest.Items is { } storage && storage.Select(entry => entry.InventoryId).Distinct().Count() != storage.Count))
                fail("snapshot.chest", $"Invalid chest contents: {item.Id}.", item.Id);
            if (item.RoomLink is { } link && (link.FurnitureId <= 0 || link.RoomId is <= 0 ||
                link.DepartureMilliseconds is < 0 || link.EffectId is < 0 ||
                link.Destination is { } destination && (destination.X < 0 || destination.Y < 0 || !double.IsFinite(destination.Z))))
                fail("snapshot.room_link", $"Invalid room-link destination: {item.Id}.", item.Id);
            if (!double.IsFinite(item.Z) || !double.IsFinite(item.Height) || item.Height < 0 || item.Width <= 0 || item.Length <= 0 ||
                item.Width > Math.Max(initial.Width, initial.Height) || item.Length > Math.Max(initial.Width, initial.Height) || item.Direction is < 0 or > 7 || item.StateCount is <= 0 ||
                item.FleeDirection is not (null or 0 or 2 or 4 or 6) ||
                item.Directions is { } directions && (directions.Count == 0 || directions.Any(direction => direction is < 0 or > 7) || directions.Distinct().Count() != directions.Count))
                fail("snapshot.furniture", $"Invalid furniture geometry: {item.Id}.", item.Id);
            if (item.Clock is { } clock && (clock.ElapsedMilliseconds is < 0 or > 5999500 ||
                clock.ElapsedMilliseconds % 500 != 0 || clock.Running && !clock.Initialized || item.State != clock.ElapsedMilliseconds / 1000))
                fail("snapshot.clock", $"Invalid clock state: {item.Id}.", item.Id);
            if (!Enum.IsDefined(item.Stance) || item.States is { } states &&
                (!states.ContainsKey(item.State) || states.Any(pair => pair.Key < 0 || item.StateCount is { } count && pair.Key >= count ||
                    pair.Value is null || !double.IsFinite(pair.Value.Height) || pair.Value.Height < 0 || !Enum.IsDefined(pair.Value.Stance))))
                fail("snapshot.furniture_states", $"Invalid or incomplete current-state geometry: {item.Id}.", item.Id);
        }
        foreach (var user in users.Values)
        {
            if (user.WiredContacts is { } contacts && (user.IsPet || contacts.Count > options.MaximumFurniture ||
                contacts.Any(id => id == 0) || contacts.Distinct().Count() != contacts.Count))
                fail("snapshot.wired_contacts", "Wired contact history requires a human or bot and distinct nonzero furniture IDs within the simulation limit.", user.Id);
            if (user.Activity is { } activity && invalid_activity(activity))
                fail("snapshot.activity", "Invalid avatar activity metadata: " + user.Id);
            if (!double.IsFinite(user.Z) || user.Direction is < 0 or > 7 || user.Index is < 0 || user.IsBot && user.IsPet || !Enum.IsDefined(user.Stance) ||
                user.ActionHeight is { } action_height && (!double.IsFinite(action_height) || action_height < 0) ||
                user.Freeze is { } freeze && (!user.Frozen || freeze.PreviousEffect < 0 || freeze.EffectId < 0) ||
                user.ClickSettings is { } click_settings && (click_settings.UserOption is < 0 or > 2 || click_settings.FurniOption is < 0 or > 1) ||
                user.Follow is { } follow && (!user.IsBot || follow.Revision <= 0 ||
                    !users.TryGetValue(follow.UserId, out var followed) || followed.IsBot || followed.IsPet ||
                    !nodes.TryGetValue(follow.ActionId, out var follow_action) || follow_action.Name != "BotFollowAvatar" || follow.Stopping && follow.Arrived) ||
                user.Delivery is { } delivery && (!user.IsBot || user.Follow is not null || delivery.Revision <= 0 || delivery.HandItem < 0 ||
                    !double.IsFinite(delivery.TargetZ) || !users.TryGetValue(delivery.UserId, out var recipient) || recipient.IsBot || recipient.IsPet ||
                    !nodes.TryGetValue(delivery.ActionId, out var delivery_action) || delivery_action.Name != "BotGiveHandItem"))
                fail("snapshot.user", $"Invalid user snapshot: {user.Id}.");
            if (user.Delivery is not null)
                note(WiredSimulationSeverity.Uncertain, "snapshot.pending_delivery", "The room records a pending delivery but not its scheduled work. Use Fork to preserve an active delivery.");
        }
        foreach (var node in nodes.Values.ToArray())
        {
            validate_sources(node);
            if (node.SavedSnapshot is { } snapshot && (node.SavedFurniture is not null || snapshot.Count > options.MaximumFurniture ||
                snapshot.Any(item => item is null || item.Id == 0 || item.Direction is < 0 or > 7 || item.X.HasValue != item.Y.HasValue || item.Z is { } z && !double.IsFinite(z)) ||
                snapshot.Select(item => item.Id).Distinct().Count() != snapshot.Count))
                fail("snapshot.saved_furniture", "SavedSnapshot requires unique nonzero IDs, valid optional components, and no simultaneous legacy SavedFurniture value.", node.FurnitureId);
            if (node.MovementDirections?.Any(pair => pair.Value is < 0 or > 7 || !furniture.ContainsKey(pair.Key)) == true)
                fail("snapshot.movement_directions", "Movement directions require existing furniture and direction codes 0..7.", node.FurnitureId);
            if (!furniture.ContainsKey(node.FurnitureId)) { fail("snapshot.node", "A wired node has no furniture.", node.FurnitureId); continue; }
            var definition = WiredFormRegistry.Find(node.Category, node.Code);
            if (definition is null) { note(WiredSimulationSeverity.Unsupported, "form.unknown", $"Unknown {node.Category}/{node.Code}.", node.FurnitureId); continue; }
            var form = WiredFormRegistry.Draft(node.Category, node.Code);
            foreach (var field in node.Fields)
            {
                try
                {
                    if (definition.Fields.FirstOrDefault(value => value.Name == field.Key) is not { } descriptor)
                        throw new ArgumentException($"Unknown field {field.Key}.");
                    if (descriptor.Writable && active_field(node, field.Key)) form.SetField(field.Key, field.Value);
                }
                catch (ArgumentException error) { fail("form.invalid", error.Message, node.FurnitureId); }
            }
            var fields = WiredSimulationNode.From(form).Fields.ToDictionary(StringComparer.Ordinal);
            foreach (var field in definition.Fields.Where(value => (!value.Writable || !active_field(node, value.Name)) && node.Fields.ContainsKey(value.Name)))
            {
                var value = node.Fields[field.Name];
                if (field.Kind == WiredFormFieldKind.Integer && value.Integer is null)
                    fail("form.invalid", "A derived integer field requires an integer value: " + field.Name, node.FurnitureId);
                else if (field.Kind is WiredFormFieldKind.Text or WiredFormFieldKind.Variable && value.Text is null)
                    fail("form.invalid", "An inactive text field requires a text value: " + field.Name, node.FurnitureId);
                else fields[field.Name] = value;
            }
            nodes[node.FurnitureId] = node with { Fields = fields };
            if (node.Name == "EchoVariable" && variables.TryGetValue(node.Text("reference_variable"), out var reference) &&
                (reference.Availability == 21 || reference.DefinitionFurnitureId is { } reference_box &&
                    nodes.TryGetValue(reference_box, out var reference_node) && reference_node.Name == "EchoVariable"))
                fail("variable.echo_reference", "An Echo variable cannot refer to itself or another Echo variable.", node.FurnitureId);
            if (!WiredSimulationRules.Supports(node)) note(WiredSimulationSeverity.Unsupported, "rule.unimplemented", $"No execution model for {node.Name}.", node.FurnitureId);
            if (!WiredDelay.IsValid(node.Delay)) fail("form.delay", "Action delay must be 0..120 half-second pulses or negative milliseconds in steps of 50, up to 60 seconds.", node.FurnitureId);
            if (node.Quantifier is < 0 or > 3) fail("form.quantifier", "Condition quantifier is outside 0..3.", node.FurnitureId);
            if (node.Name == "DateRangeActive" && (!node.Fields.ContainsKey("start_timestamp") || !node.Fields.ContainsKey("end_timestamp")))
                note(WiredSimulationSeverity.Unsupported, "date.open_range", "Open date ranges require explicit bound-presence metadata.", node.FurnitureId);
            foreach (long id in (node.Furniture ?? []).Concat(node.SecondaryFurniture ?? []))
                if (!furniture.ContainsKey(id)) note(WiredSimulationSeverity.Warning, "reference.missing", $"Referenced furniture {id} is absent.", node.FurnitureId);
                else if (furniture[id].Temporary) fail("reference.temporary", "Temporary furniture cannot be saved in a Wired furniture selection; select it dynamically.", node.FurnitureId);
        }
        if (status != WiredSimulationStatus.Faulted) validate_progress();
        foreach (var node in nodes.Values.Where(node => node.Name is "VariableAddonShare" or "VariableAddonImport"))
            if (node.Flag("text_connectors") || node.Flag("subvariable_creators") || node.Flag("variable_fx"))
                note(WiredSimulationSeverity.Uncertain, "sharing.external", "Enabled sharing/import requires the resolved remote definitions, connectors and FX in the snapshot. Synchronization and propagation are not simulated.", node.FurnitureId);
        if (status != WiredSimulationStatus.Faulted) bind_external_variables();
        if (status != WiredSimulationStatus.Faulted) bind_text_connectors();
        foreach (string missing in initial.Incomplete ?? []) note(WiredSimulationSeverity.Uncertain, "snapshot.incomplete", missing);
    }

    private static bool active_field(WiredSimulationNode node, string field) => (node.Name, field) switch
    {
        ("VariableLevelUp", "maximum_level") => node.Number("mode") == 2,
        ("VariableLevelUp", "step_or_first_xp" or "maximum_or_increase") => node.Number("mode") is 1 or 2,
        ("VariableLevelUp", "level_mapping") => node.Number("mode") == 0,
        _ => true
    };

    private void enqueue(long at, Frame frame, long? action = null, long? trigger = null, bool external = false, bool immediate = false, bool clock_tick = false, bool timer_reset = false, Teleport? teleport = null, BotWalk? bot_walk = null, BotArrival? bot_arrival = null, BotHandoff? bot_handoff = null, ProjectileStep? projectile = null, TradeTimeout? trade_timeout = null, RoomDeparture? room_departure = null)
    {
        if (queue.Count >= options.MaximumPendingEvents) { fail("simulation.pending_limit", "Too many scheduled events.", action ?? trigger); throw new SimulationFailure(); }
        var item = new Work(at, ++sequence, frame, action, trigger, external, clock_tick, timer_reset, teleport, bot_walk, bot_arrival, bot_handoff, projectile, trade_timeout, room_departure);
        queue.Enqueue(item, (at, immediate ? --immediate_sequence : item.Sequence)); peak_pending = Math.Max(peak_pending, queue.Count);
    }

    private void charge(Frame frame, long amount, long? id = null)
    {
        work_units = checked(work_units + amount);
        long root = root_work.GetValueOrDefault(frame.Root) + amount;
        root_work[frame.Root] = root; peak_work = Math.Max(peak_work, root);
        if (work_units > options.MaximumWork) { fail("simulation.work_limit", "The deterministic work limit was reached.", id); throw new SimulationFailure(); }
    }

    private void note(WiredSimulationSeverity severity, string code, string message, long? id = null) => diagnostics.Add(new(severity, code, message, now, id));
    private void fail(string code, string message, long? id = null)
    { note(WiredSimulationSeverity.Error, code, message, id); status = WiredSimulationStatus.Faulted; }
    private void unsupported(WiredSimulationNode node, string? detail = null)
    {
        note(WiredSimulationSeverity.Unsupported, "rule.unsupported", detail ?? $"No verified execution rule for {node.Category}/{node.Code} ({node.Name}).", node.FurnitureId);
        status = WiredSimulationStatus.Faulted; throw new SimulationFailure();
    }
    private void record(string kind, long? id, string message, IReadOnlyList<long>? targets = null, long? before = null, long? after = null)
    {
        trace_sequence++;
        if (trace.Count < options.MaximumTrace) trace.Add(new(trace_sequence, now, kind, id, message, targets?.ToArray(), before, after));
        else trace_truncated = true;
    }
    private uint next_random()
    { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return random; }
    private long choose(IReadOnlyList<long> values) => values[(int)(next_random() % values.Count)];
    private sealed class SimulationFailure : Exception;

    private double floor(int x, int y) => x < 0 || y < 0 || x >= initial.Width || y >= initial.Height ? -1 : initial.Floor?[y * initial.Width + x] ?? 0;
    private static bool covers(WiredSimulationFurniture item, int x, int y)
    {
        int width = item.Direction % 4 == 2 ? item.Length : item.Width;
        int length = item.Direction % 4 == 2 ? item.Width : item.Length;
        return x >= item.X && x < (long)item.X + width && y >= item.Y && y < (long)item.Y + length;
    }
    private bool fits(WiredSimulationFurniture item, int x, int y)
    {
        int width = item.Direction % 4 == 2 ? item.Length : item.Width;
        int length = item.Direction % 4 == 2 ? item.Width : item.Length;
        if (x < 0 || y < 0 || (long)x + width > initial.Width || (long)y + length > initial.Height) return false;
        for (int row = y; row < y + length; row++)
            for (int column = x; column < x + width; column++)
                if (floor(column, row) < 0) return false;
        return true;
    }
    private IReadOnlyList<WiredSimulationNode> siblings(WiredSimulationNode node)
    {
        var item = furniture[node.FurnitureId];
        return nodes.Values.Where(value => furniture.TryGetValue(value.FurnitureId, out var other) && other.X == item.X && other.Y == item.Y).OrderBy(value => value.FurnitureId).ToArray();
    }

    private void stimulus(Frame frame)
    {
        if (frame.Event.Kind == WiredSimulationEventKind.VariableObserved)
        {
            charge(frame, 1);
            observe_variable(frame);
            return;
        }
        if (frame.Event.Kind == WiredSimulationEventKind.Hover)
        {
            hover_fx(frame.Event.Hover!);
            return;
        }
        charge(frame, 1);
        var input = frame.Event;
        record("event", input.FurnitureId, input.Kind.ToString());
        if (!apply_activity(input)) return;
        if (input.Kind is WiredSimulationEventKind.LinkMessage or WiredSimulationEventKind.LinkDataframe) receive_link(frame);
        if (!frame.GameNotification && input.Kind is WiredSimulationEventKind.GameStarts or WiredSimulationEventKind.GameEnds)
        {
            if (initial.Game is not { } game) { fail("game.metadata_missing", "Game lifecycle events require explicit Game metadata."); return; }
            if (input.Kind == WiredSimulationEventKind.GameStarts) start_game(game.ClockId, false);
            else end_game(game.ClockId, false);
        }
        if (input.Kind is WiredSimulationEventKind.OfferTrade or WiredSimulationEventKind.AcceptTrade or WiredSimulationEventKind.ConfirmTrade)
        {
            trade_input(frame);
            return;
        }
        if (input.Kind == WiredSimulationEventKind.ApiRequest)
        {
            if (input.Api is null) { fail("api.request_missing", "API stimuli require a request."); return; }
            api_responses.Add(api_request(input.Api, frame));
            record("api.response", null, "Offline API contract evaluated; no network request was sent.");
            return;
        }
        if (input.Kind == WiredSimulationEventKind.Start)
        {
            if (started) { fail("event.duplicate_start", "Start may only initialize timers once per simulation."); return; }
            started = true;
            timer_origin ??= now;
        }
        if (input.Kind == WiredSimulationEventKind.Walk && input.UserId is { } id && users.TryGetValue(id, out var user))
        {
            if (input.X is not { } x || input.Y is not { } y) { fail("event.position", "Walk requires x and y."); return; }
            if (user.X == x && user.Y == y) { users[id] = user with { NextStep = null }; return; }
            if (user.Frozen || !walkable(x, y, user.Id)) { record("blocked", id, "The walk destination is blocked."); return; }
            var previous = wired_contacts(user);
            users[id] = located(user, x, y) with { NextStep = null };
            dispatch(frame with { Event = input with { Kind = WiredSimulationEventKind.Walk, X = user.X, Y = user.Y, Furniture = previous, Change = -1 } });
            frame = frame with { Event = input with { Furniture = floor_contacts(users[id]) } };
            remember_contacts(id, frame.Event.Furniture!.ToArray());
        }
        if (input.Kind == WiredSimulationEventKind.UseFurniture && input.FurnitureId is { } used && furniture.TryGetValue(used, out var item))
        {
            set_state(item, next_state(item), frame);
            if (item.ClassName is "wf_antenna1" or "wf_antenna2")
                enqueue(now, frame with { Event = new(WiredSimulationEventKind.Signal, now, input.UserId, item.Id, Furniture: [], Users: []) }, external: true);
        }
        if (input.Kind == WiredSimulationEventKind.Leave && input.UserId is { } leaving)
        { dispatch(frame with { DepartedUser = users.TryGetValue(leaving, out var departed) ? copy(departed) : null }); users.Remove(leaving); return; }
        dispatch(frame);
        if (input.Kind == WiredSimulationEventKind.Walk && input.UserId is { } moved) refresh_followers(moved, frame);
    }

    private void dispatch(Frame frame)
    {
        var matches = new List<(WiredSimulationNode Node, Frame Frame)>();
        foreach (var node in nodes.Values.Where(value => value.Category == WiredFormCategory.Trigger).OrderBy(value => value.FurnitureId))
        {
            var candidate = frame with { Context = frame.Context.ToDictionary(StringComparer.Ordinal),
                TextValues = frame.TextValues?.ToDictionary(StringComparer.Ordinal) ?? new(StringComparer.Ordinal), Selections = [] };
            if (trigger_matches(node, candidate)) matches.Add((node, candidate));
        }
        if (frame.Event.Kind == WiredSimulationEventKind.Start) timers_initialized = true;
        foreach (var match in matches) stack(match.Node, match.Frame);
    }

    private void stack(WiredSimulationNode trigger, Frame parent, bool called = false)
    {
        var boxes = siblings(trigger);
        var frame = parent with { Stack = trigger.FurnitureId, SelectedFurniture = [], SelectedUsers = [], Moved = [],
            Selections = parent.Selections ?? [] };
        int count = visits.GetValueOrDefault((frame.Root, trigger.FurnitureId)) + 1;
        visits[(frame.Root, trigger.FurnitureId)] = count;
        if (count > options.MaximumStackVisitsPerExecution)
        { fail("profile.stack_limit", "Stack activation limit in the selected simulation profile reached; the execution is incomplete.", trigger.FurnitureId); throw new SimulationFailure(); }
        charge(frame, 1, trigger.FurnitureId); activations++;
        foreach (var addon in boxes.Where(node => node.Category == WiredFormCategory.Addon))
        {
            if (!WiredSimulationRules.Supports(addon)) unsupported(addon);
        }
        var duplicate_placeholder = boxes.Where(box => box.Name is "UsernamePlaceholder" or "VariablePlaceholder" or "FurniNamePlaceholder")
            .Where(box => box.Text("placeholder_name").Length > 0).GroupBy(box => box.Text("placeholder_name"), StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate_placeholder is not null)
            unsupported(duplicate_placeholder.First(), "Duplicate local placeholder names are rejected by configuration; relocated duplicate add-ons require separate conformance.");
        var limit = boxes.FirstOrDefault(node => node.Category == WiredFormCategory.Addon && node.Name == "ExecutionLimit");
        if (limit is not null && !limit_allows(limit, reserve: false)) return;
        var selected = selection_for(trigger, frame, []);
        frame = frame with { SelectedFurniture = selected.Furniture, SelectedUsers = selected.Users };
        if (called && boxes.FirstOrDefault(node => node.Name == "ChestItemTypeScanner") is { } called_scanner)
            unsupported(called_scanner, "Chest scanners in directly called stacks require separate conformance.");
        scan_chests(boxes, frame);
        bool[] conditions = called ? [] : boxes.Where(node => node.Category == WiredFormCategory.Condition).Select(node => condition(node, frame)).ToArray();
        var evaluation = boxes.FirstOrDefault(node => node.Category == WiredFormCategory.Addon && node.Name == "ConditionEvaluation");
        bool accepted = called || (evaluation is null ? conditions.All(value => value) : evaluate_conditions(evaluation, conditions));
        if (!accepted) record("condition.false", trigger.FurnitureId, "Stack conditions did not pass.");
        var actions_here = boxes.Where(node => node.Category == WiredFormCategory.Action && (negative_action(node) ? !accepted : accepted)).ToList();
        if (actions_here.Count == 0) return;
        if (limit is not null && !limit_allows(limit, condition_failed: !accepted)) return;
        var selection = boxes.FirstOrDefault(node => node.Category == WiredFormCategory.Addon && node.Name is "Random" or "Unseen");
        if (actions_here.Count > 1 && selection is null)
        {
            bool ordered = boxes.Any(node => node.Category == WiredFormCategory.Addon && node.Name == "ExecuteInOrder");
            if (ordered)
            {
                actions_here = actions_here.OrderBy(node => furniture[node.FurnitureId].Z).ThenBy(node => node.FurnitureId).ToList();
                if (actions_here.Where(node => !static_empty_action(node)).GroupBy(node => furniture[node.FurnitureId].Z).Any(group => group.Count() > 1))
                    uncertain("order.equal_height", "Ordered effects have equal heights; the server tie-break is unknown.", trigger.FurnitureId);
            }
            else if (actions_here.Where(node => !static_empty_action(node)).GroupBy(node => node.Delay).Any(group => group.Count() > 1))
                uncertain("order.unordered", "Multiple effects have no Execute In Order add-on; this model cannot prove their server order.", trigger.FurnitureId);
        }
        if (selection is not null && actions_here.Count > 0)
        {
            if (selection.Name == "Unseen")
            {
                if (!unseen.TryGetValue(selection.FurnitureId, out var used)) unseen[selection.FurnitureId] = used = [];
                var available = actions_here.Where(node => !used.Contains(node.FurnitureId)).ToArray();
                if (available.Length == 0) { used.Clear(); available = actions_here.ToArray(); }
                var chosen = available[(int)(next_random() % available.Length)]; used.Add(chosen.FurnitureId); actions_here = [chosen];
            }
            else actions_here = random_actions(selection, actions_here);
        }
        validate_call_dependencies(actions_here, frame);
        prepare_calls(actions_here, frame);
        record("stack", trigger.FurnitureId, accepted ? "Stack accepted." : "Condition-failure branch.", frame.SelectedFurniture);
        foreach (var action in actions_here)
        {
            if (action.Delay != 0)
            {
                long next_root = ++root_sequence;
                publication_roots[next_root] = publication_root(frame);
                enqueue(checked(now + action_delay(action)),
                    copy(frame) with { Root = next_root }, action: action.FurnitureId);
            }
            else if (!called && !frame.InlineActions) enqueue(now, frame, action: action.FurnitureId);
        }
        if (called || frame.InlineActions)
            foreach (var action in actions_here.Where(action => action.Delay == 0).Reverse())
                enqueue(now, frame, action: action.FurnitureId, immediate: true);
    }

    private static bool negative_action(WiredSimulationNode node) =>
        node.Category == WiredFormCategory.Action && node.Code is 36 or 37 or 50;

    private void uncertain(string code, string text, long id)
    {
        note(WiredSimulationSeverity.Uncertain, code, text, id);
        if (options.RequireKnownOrder) { status = WiredSimulationStatus.Faulted; throw new SimulationFailure(); }
    }
    private static bool evaluate_conditions(WiredSimulationNode addon, bool[] values)
    {
        int passed = values.Count(value => value);
        return addon.Number("mode") switch
        { 0 => passed == values.Length, 1 => passed > 0, 2 => passed < values.Length, 3 => passed == 0, -1 => compare(passed, addon.Number("comparison"), addon.Number("amount")), _ => false };
    }
}
