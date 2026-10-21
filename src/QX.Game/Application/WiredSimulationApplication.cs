using Qx.Game.WiredSimulation;
using Qx.Model;
using Qx.Model.Wired;
using Engine = Qx.Game.WiredSimulation.WiredSimulation;

namespace Qx.Game.Application;

internal sealed partial class WiredSimulationApplication : IApplicationFeature
{
    private readonly GameState game;
    private readonly IApplicationRuntime application;
    private readonly object gate = new();
    private readonly Dictionary<string, Engine> sessions = new(StringComparer.Ordinal);
    private static readonly string[] wired_form_prefixes = ["wf_trg_", "wf_act_", "wf_cnd_", "wf_slc_", "wf_xtra_", "wf_var_", "wf_proto_"];
    private bool disposed;

    public WiredSimulationApplication(GameState game, IApplicationRuntime application)
    {
        this.game = game;
        this.application = application;
        Bindings = [
            new ApplicationCallBinding<WiredSimulationCoverageRequest, WiredSimulationGuide>(WiredSimulationDescriptors.Guide, (_, _) => ValueTask.FromResult(guide())),
            new ApplicationCallBinding<WiredSimulationCreateRequest, WiredSimulationSession>(WiredSimulationDescriptors.Create, (request, _) => ValueTask.FromResult(add(new(request.Room, request.Options)))),
            new ApplicationCallBinding<WiredSimulationPlanRequest, WiredSimulationRoom>(WiredSimulationDescriptors.Plan, (request, _) => ValueTask.FromResult(WiredSimulationPlan.Apply(request.Plan, request.Room))),
            new ApplicationCallBinding<WiredSimulationRunRequest, WiredSimulationResult>(WiredSimulationDescriptors.Run,
                (request, cancellation) => ValueTask.FromResult(read(request.SessionId, engine => engine.Run(request.Events, request.AdvanceMilliseconds, request.MaximumEvents, request.Breakpoints?.ToHashSet(), cancellation)))),
            new ApplicationCallBinding<WiredSimulationSessionRequest, WiredSimulationResult>(WiredSimulationDescriptors.Inspect, (request, _) => ValueTask.FromResult(read(request.SessionId, engine => engine.Inspect()))),
            new ApplicationCallBinding<WiredSimulationSessionRequest, WiredSimulationSession>(WiredSimulationDescriptors.Fork, (request, _) => ValueTask.FromResult(read(request.SessionId, engine => add(engine.Fork())))),
            new ApplicationCallBinding<WiredSimulationSessionRequest, WiredSimulationRemoved>(WiredSimulationDescriptors.Delete, (request, _) => ValueTask.FromResult(remove(request.SessionId))),
            new ApplicationCallBinding<WiredSimulationAssertRequest, IReadOnlyList<WiredSimulationAssertionResult>>(WiredSimulationDescriptors.Assert,
                (request, _) => ValueTask.FromResult(read(request.SessionId, engine => engine.Assert(request.Assertions)))),
            new ApplicationCallBinding<WiredSimulationLeaderboardRequest, WiredSimulationLeaderboardPage>(WiredSimulationDescriptors.LeaderboardPreview,
                (request, _) => ValueTask.FromResult(read(request.SessionId, engine => engine.PreviewLeaderboard(request.FurnitureId, request.VariableId, request.Page)))),
            new ApplicationCallBinding<WiredSimulationCaptureRequest, WiredSimulationRoom>(WiredSimulationDescriptors.Capture, capture_with_variables)
        ];
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    private async ValueTask<WiredSimulationRoom> capture_with_variables(WiredSimulationCaptureRequest request, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = request with { ObjectValues = freeze_object_values(request.ObjectValues) };
        if (request.ObjectValues is { Count: > 0 })
        {
            var current = game.Wired.Snapshot;
            if (!current.CanRead) throw new InvalidOperationException("Current server-granted Wired read permission is required for object-variable readouts.");
            if (request.ObjectValues.Any(value => value.Generation != current.Generation))
                throw new ArgumentException("An object-variable readout belongs to another Wired generation.");
            bool targets_exist = game.Room.Capture(room => request.ObjectValues.All(value => value.Target switch
            {
                WiredTarget.User => room.Avatars.Count(user => user.Index == value.ObjectId) == 1,
                WiredTarget.Furni => room.FloorItems.Any(item => (long)item.Id == value.ObjectId),
                _ => true
            }));
            if (!targets_exist) throw new ArgumentException("An object-variable readout refers to an absent or ambiguous holder.");
        }
        var definitions = (request.Variables ?? []).ToDictionary(variable => variable.Id, StringComparer.Ordinal);
        var activity_names = request.Variables is null && request.ObjectValues?.Any(value => value.Target == WiredTarget.User) == true
            ? new HashSet<string>(["@dance", "@sign"], StringComparer.Ordinal) : [];
        import_reference_definitions(request.Forms ?? [], definitions);
        var echo_names = (request.Forms ?? []).Select(view => view.CreateForm()).OfType<WiredEchoVariableForm>()
            .Select(form => form.Name).ToHashSet(StringComparer.Ordinal);
        var pointer_names = (request.Forms ?? []).Select(view => view.CreateForm()).OfType<WiredPointerVariableForm>()
            .Select(form => form.Name).ToHashSet(StringComparer.Ordinal);
        var reference_names = (request.Forms ?? []).Select(view => view.CreateForm()).OfType<WiredReferenceVariableForm>()
            .Select(form => form.Name).ToHashSet(StringComparer.Ordinal);
        var context_names = (request.Forms ?? []).Select(view => view.CreateForm()).OfType<WiredContextVariableForm>()
            .Select(form => form.Name).ToHashSet(StringComparer.Ordinal);
        var needed = (request.Forms ?? []).SelectMany(view => view.Configuration.VariableIds)
            .Concat((request.Forms ?? []).SelectMany(view => view.Configuration.Context)
                .SelectMany(context => (context.Variable is { } variable ? new[] { variable.VariableId } : [])
                    .Concat(context.VariableLeaderboard?.Choices.Select(choice => choice.VariableId) ?? [])))
            .Concat((request.ObjectValues ?? []).SelectMany(snapshot => snapshot.Values).Select(value => value.VariableId))
            .Where(id => !string.IsNullOrWhiteSpace(id) && id is not "n" and not "0" && !definitions.ContainsKey(id))
            .Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (needed.Count == 0 && activity_names.All(name => definitions.Values.Any(variable => variable.Name == name && variable.Domain == WiredSourceDomain.User)) &&
            echo_names.All(name => definitions.Values.Count(variable => variable.Availability == 21 && variable.Name == name) == 1) &&
            pointer_names.All(name => definitions.Values.Count(variable => variable.Name == name && variable.RequiresExecutionContext == true) == 1) &&
            reference_names.All(name => definitions.Values.Count(variable => variable.Availability == 20 && variable.Name == name) == 1) &&
            context_names.All(name => definitions.Values.Count(variable => variable.Domain == WiredSourceDomain.Context && variable.Name == name) == 1))
            return await capture_contracts(capture(request with { Variables = definitions.Values.ToArray() }), request, cancellation).ConfigureAwait(false);
        var observed = await application.InvokeAsync<WiredVariableListRequest, WiredVariableCollectionSnapshot>(
            ApplicationMemberIds.WiredVariablesList, new(), cancellation).ConfigureAwait(false);
        foreach (var entry in observed.Variables)
        {
            var variable = entry.Variable;
            if ((!needed.Contains(variable.VariableId) && !(variable.AvailabilityType == 21 && echo_names.Contains(variable.VariableName)) &&
                !(variable.AvailabilityType == 20 && reference_names.Contains(variable.VariableName)) &&
                !(variable.VariableTarget == (int)WiredSourceDomain.Context && context_names.Contains(variable.VariableName)) &&
                !(variable.RequiresExecutionContext == true && pointer_names.Contains(variable.VariableName)) &&
                !(variable.VariableTarget == (int)WiredSourceDomain.User && activity_names.Contains(variable.VariableName))) ||
                !Enum.IsDefined((WiredSourceDomain)variable.VariableTarget)) continue;
            if (definitions.ContainsKey(variable.VariableId)) continue;
            definitions[variable.VariableId] = new(variable.VariableId, variable.VariableName, (WiredSourceDomain)variable.VariableTarget,
                variable.AvailabilityType, variable.HasValue, variable.CanWriteValue,
                captured_text(variable.TextConnector?.Select(text => ((long)text.Id, text.Value))), TextFallback: variable.TextConnectorFallback,
                CanCreateAndDelete: variable.CanCreateAndDelete, RequiresExecutionContext: variable.RequiresExecutionContext);
        }
        var result = capture(request with { Variables = definitions.Values.ToArray() });
        string[] missing = needed.Where(id => !definitions.ContainsKey(id)).Select(id => "Variable definition unavailable: " + id)
            .Concat(activity_names.Where(name => !definitions.Values.Any(variable => variable.Name == name && variable.Domain == WiredSourceDomain.User))
                .Select(name => "Activity variable definition unavailable: " + name))
            .Concat(echo_names.Where(name => definitions.Values.Count(variable => variable.Availability == 21 && variable.Name == name) != 1)
                .Select(name => "Echo variable definition is missing or ambiguous: " + name)).ToArray();
        if (missing.Length != 0) result = result with { Incomplete = [.. result.Incomplete ?? [], .. missing] };
        return await capture_contracts(result, request, cancellation).ConfigureAwait(false);
    }

    private async ValueTask<WiredSimulationRoom> capture_contracts(WiredSimulationRoom room,
        WiredSimulationCaptureRequest request, CancellationToken cancellation)
    {
        var selected = (request.Forms ?? []).SelectMany(view => view.Configuration.StuffIds.Concat(view.Configuration.StuffIds2))
            .Select(id => (long)id).ToHashSet();
        var contents = new Dictionary<long, WiredContractContents>();
        var missing = new List<string>();
        foreach (var item in room.Furniture.Where(item => selected.Contains(item.Id) && item.Contract is null &&
            item.ClassName is "wf_contract_payment" or "wf_contract_trade" or "wf_contract_reward"))
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    contents[item.Id] = await application.InvokeAsync<WiredContractOpenRequest, WiredContractContents>(
                        ApplicationMemberIds.WiredContractOpen, new(checked((int)item.Id), 5000), cancellation).ConfigureAwait(false);
                    break;
                }
                catch (RequestTimeoutException) when (attempt == 0) { }
                catch (RequestTimeoutException) { missing.Add("Contract contents unavailable: " + item.Id); }
            }
        }
        return room with
        {
            Furniture = room.Furniture.Select(item => contents.TryGetValue(item.Id, out var contract)
                ? item with { Contract = WiredManager.SnapshotOf(contract) } : item).ToArray(),
            Incomplete = missing.Count == 0 ? room.Incomplete : [.. room.Incomplete ?? [], .. missing]
        };
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; sessions.Clear(); }
    }

    private WiredSimulationSession add(Engine engine)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (sessions.Count >= 16) throw new InvalidOperationException("Delete an unused simulation before creating another; the session limit is 16.");
            string id = Guid.NewGuid().ToString("N");
            sessions.Add(id, engine);
            return new(id, engine.Inspect());
        }
    }

    private T read<T>(string id, Func<Engine, T> action)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!sessions.TryGetValue(id, out var engine)) throw new KeyNotFoundException("Unknown simulation session: " + id);
            return action(engine);
        }
    }

    private WiredSimulationRemoved remove(string id)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new(sessions.Remove(id));
        }
    }

    private static WiredSimulationGuide guide() => new(
        "Deterministic offline model under construction. Unsupported rules stop execution. No result proves server equivalence; work counters are not hotel Wired usage.",
        ["Read coverage and limitations before relying on a result.",
         "Supply Room.Game with the source ClockId, phase and known ScoreUses before simulating scoring. A running counter does not prove the game is active: Resume after Stop resumes time without restarting scoring. ScoreUses=null means unknown allowance history; an empty list means known unused allowances. Calls during Pause or without team membership still consume finite allowance. New game Start resets all team scores and allowance counters. These fields survive Fork and room export. New-game tie ranking and several-recipient or cross-user finite allowance semantics remain unverified.",
         "ScoreAchieved fires once per box per game, across eligible teams, even if that stack's conditions fail. Game.FiredScoreTriggers preserves used box IDs; null means unknown history and an empty list means all boxes are known unused. A fresh game Start clears the list. Changing that box's threshold or team rearms it; an unchanged save does not. Subtraction clamps team totals at zero. Delayed score effects use the game state at their due time, so they can survive a Stop/Start transition and award points in the new game.",
         "Capture cached room state or provide an explicit room, including typed forms, variable definitions, state counts and collision metadata.",
         "Pass confirmed GetFurniVariables/GetUserWiredVariables/GetGlobalVariables results through capture object_values to attach holder values and reconcile exact native position, altitude, direction and furniture state. Readouts must belong to the current Wired generation; user room indices are translated to account IDs. They do not contain timestamps and are not an atomic scene snapshot. FromClientDisplay marks omitted timestamps unknown and saturated values inexact. Directly constructed values retain explicitly supplied timestamp assumptions.",
         "Furniture object readouts resolve native @type aliases through supplied variable definitions: 0 normal, 1 Builders Club, 2 temporary. These populate IntrinsicType and Temporary. Exact furniture @id/@class_id and user @user_id/@index mismatches reject misaddressed reads. Packed and component geometry must agree. IntrinsicState remains separate from visual State for antennas. Consistent sub-centimetre heights are retained instead of replacing them with rounded readouts; incompatible height metadata and invalid or saturated geometry remain marked incomplete. Changes after reading still require fresh observations.",
         "Furniture @rotation is a quarter-turn index, while Furniture.Direction is measured in eighth-turns. @occupation is (X << 16) | (Y << 8) | rotationIndex. It does not contain width or length. Supply known allowed Directions before predicting a changed rotation; capture supplies recorded directions for color tiles, ducks, Lodge desks and Lodge tables. Packed moves reject unsupported rotations and invalid footprints atomically; same-tile writes preserve altitude.",
         "Leaderboard preview requires complete StoredVariables for every ranked/filter variable, including absent room users, and an Epoch for timestamps. Give, change and remove variable effects update supplied persistent stores; leaving the room does not erase them. Preview is the current logical result, not a prediction of the hotel's delayed display refresh. Capture observed variable DisplayName metadata or configure display overrides; equal sort keys remain guarded until the server tie-break is established.",
         "Leaderboard row fields follow selected columns: unselected numeric values are empty strings, unselected timestamps are zero, and unselected level/text fields are null. An Epoch is required only when timestamp columns are selected. Variable CanCreateAndDelete controls presence changes separately from Writable, which controls numeric assignment. Presence-only variables use -1 when granted; the numeric initialization operand is ignored.",
         "Node.InputSources preserves the captured per-slot allowed sources and defaults. Sources outside that definition fail validation before execution; omitted selections use its defaults. Existing-node build edits retain this definition. A new offline node has no measured source permissions until a live configuration is captured; enum membership alone does not establish availability on a particular Wired box.",
         "Stored Wired arithmetic is signed 64-bit, but the client variable inspector clamps values to signed 32-bit bounds. MayBeClamped flags ambiguous boundary values in native value snapshots. Construct observed values with WiredSimulationValue.FromClientDisplay so boundary reads remain IsExact=false and cannot silently drive calculations. Replace them with a verified full value, for example from a controlled numeric Wired log, before simulating. A directly constructed WiredSimulationValue represents an explicitly known value.",
         "Apply a native build plan offline if required, then create a simulation session.",
         "Inspect and Run return detached room data. Editing exported furniture, Link bindings, API address mappings or shared placeholders does not change the current session. Create a new session from the edited Room to test those changes; use Fork to preserve pending execution work.",
         "Classic normal and fast teleports refresh retained human Wired contacts even when XY is unchanged, emitting only changed contacts. Direct MoveUserToFurni on the same XY preserves prior contacts. This distinction matters after a support was lowered without a horizontal move; position readouts alone cannot supply the retained contact history.",
         "Altitude selectors and conditions compare rounded centimetres, with half values upward like the native altitude variable: 14.125 matches 1413. Compare using the supplied geometry precision; do not use ties-to-even rounding or a raw floating-point threshold comparison.",
         "Inject events at absolute virtual milliseconds. Advance time or step with maximum_events=1. Inspect diagnostics and traces.",
         "A normal one-credit/one-floor-furniture static trade can receive OfferTrade, AcceptTrade and ConfirmTrade events with a Trade payload carrying the active offer Revision. OfferTrade Credits is an externally validated payment value, not an inventory lookup or wallet debit. AcceptTrade starts the native three-second confirmation countdown. Settlement requires captured credit chest Capacity; unsupported amounts, inventory valuation and early confirmation are guarded.",
         "StateChanged, VariableChanged, ScoreChanged and UserAction are notifications of an already represented change; they do not apply external mutations to snapshot state. Supply the changed state in the room. Walk supplies an arrival, not a hotel pathfinding request. Roller transport, one-way entry and full navigation are not yet simulated.",
         "Capture decodes stored furniture rows from supported Wired forms into Node.SavedSnapshot. Z uses room-height units after conversion from the wire's centimetres. A null component means it was not stored; zero is an actual stored value. A null snapshot list is unknown, while an empty list is known empty. Legacy SavedFurniture accepts complete caller-supplied rows; do not supply both representations. A real configuration save refreshes enabled snapshot components, and an unchanged plan without capture_snapshot preserves its prior rows.",
         "Supply Furniture.States for state-dependent walkability, height, stacking and posture. Missing entries stop execution. Sitting and lying use base Z plus a separate User.ActionHeight; do not add the action height into Z. Captured static furniture data cannot infer every gate state.",
         "FreezeUser applies effect options 0..4 as avatar effects 218, 12, 11, 53 and 163; option zero is not no-effect. User.Freeze preserves the prior effect and cancellation policy across snapshots. Reapplying a freeze replaces its visual and keeps the original prior effect. UnfreezeUser releases movement and restores that prior effect. Freezing over an existing nonzero effect and changing effects while frozen remain guarded.",
         "A freeze with CancelOnTeleport is released on classic teleport arrival, not when the teleport starts. MoveUserToFurni preserves the freeze even when that flag is true. Cached avatar data does not reveal server freeze state: captured Frozen=false is not evidence of freedom to walk; supply explicit Frozen and Freeze metadata for an already frozen actor.",
         "MoveUser moves an idle human one adjacent tile while retaining facing unless rotation is configured. A blocked move can still rotate a standing user. Rotation-only actions do not turn a seated avatar; moving off a chair allows the requested rotation, while moving onto one adopts its facing. A frozen user can still be moved or rotated by this action. Ordinary walking remains blocked by the freeze.",
         "MoveUser, MoveUserToFurni and MoveFurniToFurni report visual interpolation independently from logical arrival. NoMoveAnimation produces duration zero and takes precedence over AnimationTime. The tested canceled furniture transfer sends a regular position update without a WiredMovements entry; the simulator's zero-duration MovementAnimation is a semantic record, not a predicted raw packet.",
         "MoveAsGroup anchors the selected one-tile furniture at its minimum X/Y and applies a common translation toward the target plus offsets. Each member gets its own destination stacking height unless KeepAltitude is enabled. A blocked member does not cancel the others, but CanPerformMove rejects the whole stack when any member is blocked. MovementPhysics and animation modifiers apply to disjoint groups. Repeated overlapping source/destination layouts produced different server outcomes, so such layouts are rejected as unresolved. Use a clear disjoint staging region for predictable group translation.",
         "VariableAge reads CreatedAt or UpdatedAt from WiredSimulationValue using the same virtual millisecond origin as simulation events. A live value snapshot without known timestamps cannot establish age. Same-value assignments refresh UpdatedAt and can fire VariableUpdate with the unchanged mask; GiveVariable overwrite resets both timestamps and fires creation. Avoid feedback loops that listen to unchanged writes unless bounded. Calendar month/year ages and exact threshold equality are not proven.",
         "MoveUserToFurni changes the logical user position immediately, without a teleport avatar effect; MovementAnimation records the separate default 500 ms visual interpolation. A blocked target prevents this direct transfer. It can follow classic teleport arrival while the earlier effect finishes; transfers before that arrival remain guarded. Source-exit and destination-entry triggers can chain further direct transfers, even when client packets omit an intermediate position. User.NextStep is captured from live movement but is only the next walking tile, not the final path target; active-walking transfer modes remain guarded.",
         "Classic TeleportToFurniture is delayed and temporarily changes avatar effect. Supply explicitly calibrated Room.TeleportTimings.Normal and Fast with ArrivalMilliseconds, EffectMilliseconds and EffectId; these are scenario inputs, not inferred server constants. ArrivalMilliseconds means logical contact time: tested entry/exit witnesses fired about 500 ms before the ordinary client position packet. Use those witnesses for calibration. Landing retains the original target XY when the target moves, but reads current surface height. The tested blocked target fallback checks north, northeast, east and southeast; further blocked fallback paths remain guarded. Fork preserves pending arrivals and effect restoration; exporting Room alone does not preserve them.",
         "MoveFurniToFurni uses independent source slots: Picked selects the primary moving furniture, SecondaryPicked selects SecondaryFurniture as the target. Merely filling SecondaryFurniture does not change the source selector. Single-item endpoint transfer applies destination stacking and MovementPhysics rather than walking along intermediate tiles; animation is separate from arrival.",
         "Multi-source or multi-target MoveFurniToFurni needs FurnitureTransferChoices for strict replay. Each choice has ActionId and ordered Steps of FurnitureId/TargetId, assigning every selected source exactly once to a selected target. Choices are consumed by ambiguous invocations only, survive Fork, and represent a particular possible branch. Saved selections and movement packet order cannot supply the real execution order. Without explicit choices strict mode reports movement.transfer.choice; permissive mode samples a seeded branch and remains Uncertain. A batch may send different sources to different targets, and earlier moves can change later stacking and blocking. Enumerate admissible branches when checking a design instead of treating one sampled result as a guarantee.",
         "BotMove pathfinding uses an octile estimate with lower remaining-distance tie breaks; the prior Manhattan estimate could prefer a longer gate detour. In twelve single-bot controls, a zero-height one-tile gate closing after movement starts causes a reroute toward the original destination, while opening preserves the chosen route in that layout. Thirty-six target-gate controls additionally establish no movement toward an initially closed target, even if opened later, and stopping when the target closes during approach. Diagonal approaches first take the verified preferred cardinal fallback; a straight northward approach stops before the gate. Supply complete state-dependent geometry. Dynamic gate handling for grouped walkers, follow/delivery, mixed occupants and other elevations remains unverified.",
         "A single standing bot can walk or teleport to an unoccupied one-tile seated destination. Sixteen chair controls in four rotations match the chair's base Z, Sit posture, ActionHeight, facing and arrival marker. Set Walkable from the combined stand/sit/lay flags, not CanStandOn alone. BotMove allows the chair only as its final step and checks the base height independently from posture height. Unmatched seated supports, overlapping or moved walking targets, larger or lying walking targets and grouped seated walks require further conformance.",
         "BotMove and BotTeleport groups of two or three need BotMovementChoices for strict replay. Supply ActionId and ordered Steps of BotId/TargetId, covering each selected bot exactly once and every selected destination at least once. With three bots and two targets, one target receives two bots. A bot-occupied teleport target permits overlap, including a walking bot at arrival. Six human controls also permit an idle standing human already on the target or moved there during the teleport; nine additional controls cover overlapping two- and three-bot groups on that human target. Active, seated, frozen and affected human occupants remain guarded. One shared walking target has a single winner. A blocked final diagonal uses the verified preferred cardinal fallback and stops; an unavailable fallback is guarded. Order and pairing are explicit branch assumptions, not a prediction from saved selection order. Pending groups and choice cursors survive Fork. An intermediate tile occupied by another selected bot or an idle standing bot from an independent stack causes a path recalculation to the original goal when reachable. An occupied final target stops the bot, even if the blocking bot still has its completed teleport effect. Nine human controls also establish rerouting and stopping for an idle standing human moved into the path; these passed with room walkthrough enabled. Walking still guards unreachable reroutes, unselected navigating bots, active, seated, frozen or affected human blockers, pets, groups beyond three, larger furniture and initially occupied group targets. Calibrate clock phases or test multiple phases for competing teleport/walk events; a shared server tick can change whether a detour is needed. Verify every admissible assignment/order when checking a design; matching one recorded branch is not a guarantee for every live execution.",
         "A seated single bot can walk to a standing destination when its one-tile chair, base Z and ActionHeight match the supplied posture. Departure clears Sit and ActionHeight, emits the retained chair's WalksOffFurniture contact and later the destination contact. The first step uses the chair base rather than its posture height. Sixteen controls compare walking and teleporting departures in four chair rotations. Overlapping seats, seated-to-seated walks, lying starts and inconsistent posture metadata remain unverified.",
         "CarrierContactChoices supplies ActionId and UserId for each ambiguous shared-carrier contact transition. The supported shared source has one idle standing human and one idle standing bot with distinct known room indices on a one-tile walkable furniture. A movement can produce one walk-trigger context for that pair; its selected user cannot be reconstructed from packet or snapshot order. Supply a recorded branch or explore each eligible user. Entries are consumed only when a shared movement changes contacts and survive Fork. Missing choices report movement.shared_contact_choice; permissive sampling remains Uncertain. The selected walk context is separate from the lower-index occupant whose logical altitude is updated when a shared support moves vertically or moves away without carrying.",
         "Larger bot carrying supports one idle standing bot on walkable one-by-two, two-by-one and two-by-two sources with a disjoint horizontal destination and unchanged direction. Verified positions include the far corner of a two-by-two stage and the outer cells of a rectangular table in both orientations. Relative offsets are retained by furniture transfer, relative movement, single-member groups and packed position/occupation. Carry mode 0 requires direct support; mode 1 includes an upper support. Supply WiredContacts when an earlier height change left different contact history. Larger footprints, overlap, physics, curves and vertical movement on these larger sources remain guarded.",
         "User.WiredContacts supplies a human or bot avatar's prior Wired contact furniture independently from current support geometry. Null uses the geometry-based initial assumption; an empty list means known no prior contacts. A height-only support change can retain the old contact: lowering an entry pad under a stage changes avatar altitude to the stage but the next horizontal carry still emits leave-pad and enter-stage callbacks. Human controls cover direct user-to-furniture entry, native altitude writes and the height effect, at anchor and outer cells. Walking, teleport arrival and horizontal carrying update modeled contacts; height-only carrying preserves them. Capture cannot reconstruct this history from position or native altitude readouts. Pet contact overrides remain unsupported.",
         "For PlaceFurni using several furniture initial-value holders, SpawnValueChoices selects one known branch per action, shared by all copies and destinations using ActionId, Domain and HolderId. The chosen holder must belong to that action's current source slot; invalid choices fail before any copy is created. Only ambiguous invocations consume choices; Fork retains an independent cursor. Without a choice, strict mode reports temporary.value_source.choice before placement, while permissive mode samples one possible holder and remains Uncertain. Enumerate the admissible holders to check every possible result. Repeated live controls with two templates and one or two destinations retained one shared value per activation. Sampling does not model the hotel's random distribution; multiple user holders still require separate conformance.",
         "RandomChoices uses AddonId and ordered ActionIds for each supplied Random activation. These are replay assumptions, not a prediction of server randomness. RandomHistory stores oldest-to-newest execution batches for each AddonId. Complete=true declares the prior window known, including a fresh empty history; false preserves an observed suffix with unknown earlier executions. Current settings do not reveal this history. Ambiguous runs without choices report random.choice; an insufficient known history reports random.history_unknown. Permissive sampling remains Uncertain.",
         "LimitHistory supplies each ExecutionLimit add-on's successful Activations and separate ConditionFailures timestamps relative to the snapshot time origin. A full successful window blocks either branch; condition-failure effects consume only their own capacity. Complete=true declares both histories known, with an omitted ConditionFailures list meaning empty. For an observed suffix use Complete=false and ObservedSince<=0. Missing older history reports limit.history_unknown until its window expires. Called stacks use the same successful window as direct activations. Export rebases both histories; Fork preserves independent copies.",
         "User.Activity separates Dance from Sign. Dance=0 means stopped. SignKnown=true with Sign=null means no active sign; Sign=0 is the real zero sign. Captured status fragments cannot establish sign absence, while complete @sign readouts can establish a point observation. SignRemainingMilliseconds is explicit calibration, measured from SignObservedAt; aged signs without a known lifetime report action.sign_lifetime. Export and Fork preserve remaining lifetimes and observation age. A UserAction event may include an Activity patch: supplied Dance changes that component, and SignKnown=true replaces the sign component at event time without erasing dancing. Text supplies a decimal sign or 'dance N' for filtered notifications. Without Activity, notifications do not alter stored state.",
         "A furniture-to-furniture transfer may carry one idle human using CarryUsers, or leave the user behind and reevaluate their old surface. MovementPhysics still controls furniture and user blocking. A permitted move onto a stationary user's tile updates their support height/posture without a walk event. For two equal contact planes, the tested standing user changes from the carrier tile to the stationary tile; a seated user retains the carried chair. These are support contacts, not all overlapping furniture. Multiple stationary ties, affected users, bots/pets, active walking, lying and occupied transfer batches require further conformance.",
         "Larger single furniture transfers use the selected destination's anchor, preserve relative avatar offsets, and check every rotated footprint cell for floor, blockers and support. Partial overlap with the source's old footprint can preserve altitude because the old source top participates in support selection. A higher external top overrides it. Carried standing users on nonwalkable furniture retain at least the new carrier base altitude; this is not permission to stand on its top. Flat 2x1, rotated 1x2 and 2x2 cases have live controls. Uneven base floors, larger batches and larger projectile/curve combinations remain guarded.",
         "Relative movement and single-member group movement also retain at least the solid nonwalkable carrier base under an idle standing human, including non-anchor cells. Reevaluate contacts after that support correction: the tested elevated helper is left and the large carrier is entered. CarryUsers mode 0 does not take a human standing on a different elevated helper; mode 1 does. Transfer, relative and group controls cover large and rotated footprints, KeepAltitude and larger stationary targets. These controls do not establish multi-member occupied groups or crowded contact ties.",
         "Optional Room.BotTiming.TeleportClock supplies TickMilliseconds and NextTickOffsetMilliseconds for the bot teleport scheduler. With this clock, arrival and effect durations start at the strictly next tick, even when activation lands exactly on a tick. The measured room used a 500ms interval, 1000ms base arrival and 3000ms base effect; calibrate the phase from server log timestamps. Without TeleportClock, existing fixed calibrated delays remain unchanged. Snapshot export rebases the phase; Fork retains pending arrivals and effect ownership. An active owned teleport effect is not renewed by a successor teleport, and effect expiry does not prove that its last arrival finished.",
         "Counter clocks require Furniture.Clock with half-second elapsed time, Running and Initialized, plus a calibrated room ClockTickOffsetMilliseconds in 0..499. Visible furniture state truncates half seconds and does not reveal running or initialization history. A running clock keeps future tick work pending until stopped; inspect its state and diagnostics instead of expecting a completed run.",
         "Room.Timer is separate from counter furniture. Supply its @wired_timer Value and next TickOffsetMilliseconds in 0..499; ResetPending describes a reset already requested but not yet applied. Start without timer metadata explicitly initializes a fresh virtual timer. Reset Timers takes effect on the next room tick: immediate effects still read the old value, repeaters fire at the new zero tick, and one-shot thresholds restart. Already delayed effects remain queued. TimeElapsedMore and TimeElapsedLess compare the encoded threshold inclusively against timer ticks, not simulation milliseconds. Fork preserves delayed work; exporting only Room preserves timer metadata but not unrelated queued effects.",
         "Furniture.IntrinsicState supplies an intrinsic @state that differs from the visible State. For the tested small antenna, visual state toggles on each emitted signal while intrinsic @state stays zero. Signals also cause state-change triggers; per-payload signal and state events are ordered separately.",
         "MoveToDirection requires Node.MovementDirections keyed by selected furniture ID. Empty means a known fresh effect; null means the live hidden history is unknown. Snapshot/fork preserves this history. Manual furniture movement does not reset it, and different movement effects retain separate directions.",
         "CanPerformMove preflights the current stack without moving its furniture or changing retained directions. Independent directional or relative effects must all have a destination; fixed blocked turns can supply one. The tested quantifier codes 0..3 do not change this rule. Delayed effects are checked at trigger time, but their later movement still depends on later geometry. Relative movement preflight also checks destination floor, MoveThroughFurni, BlockByFurni and MoveThroughUsers, with recorded single-human destination and seated-carry cases. Shared targets, intersecting paths, multiple-actor and pet/bot combinations, other movement types and random effect selection remain unverified.",
         "ApiRequest evaluates the recorded Web API contract offline and returns ApiResponses. Supply explicit room API permissions and API-to-room furniture addresses. Profile PATCH accepts null to remove user or furniture values while preserving omitted names; global profile values cannot be null. All requested changes and deletion permissions are validated before mutation. This partial model sends no HTTP requests and does not predict HTTP throttling, paging, user-profile existence or profile deletion. Bulk deletion requires complete holder data and an explicit notification policy; documented REST behavior does not establish exact server event timing.",
         "Temporary-copy altitude is a base plus AltitudeOffset/100: mode 0 uses the highest support over the complete rotated footprint, mode 1 the source base, mode 2 the custom reference base. Mode 2 does not become absolute merely because XY uses the source location. Every destination cell is checked for its highest surface's stacking permission; a high copy altitude alone does not bypass a blocking gate. Unknown reference or support cases remain explicit diagnostics.",
         "Furniture @type is 0 for an inventory item, 1 for a Builders Club item and 2 for a temporary copy in the recorded controls. Set IntrinsicType from an object-variable query or known placement provenance for permanent items; never infer it from the class or ID. Engine-created copies are Temporary with IntrinsicType=2. Variable selectors used as filters evaluate only their incoming selection, so unrelated unknown metadata outside it does not block the result.",
         "A temporary copy immediately following a source state change is unresolved: the server can retain the previous state. Strict simulation stops instead of certifying that result. A one-pulse delay matched the tested circuit, not every possible server scheduling case.",
         "Normal predictions use RequireKnownOrder=false: uncertain ordering and supported random branches produce a deterministic candidate with visible diagnostics instead of stopping immediately. Change Seed to explore another candidate. RequireKnownOrder=true enables strict checking. Neither mode invents unsupported operations or missing required metadata.",
         "Status Completed means the requested simulation finished. Completed results with uncertainty are useful predictions, not server guarantees. Passed is the stricter check requiring only informational diagnostics; Passed=false alone does not mean no prediction was produced. Inspect diagnostics and predicted state together.",
         "BotGiveHandItem uses bot input 0 and human recipient input 1. Empty optional bot name bypasses the bot. Named delivery holds the item, walks if necessary, hands it over and clears the bot hand. A moved recipient still receives it, but BotAvatarReached fires only when the recipient is adjacent at handoff. The original delivery destination is retained. Distant delivery requires an explicit BotTiming delivery start phase and arrival delay. Equal-cost destinations are uncertain unless a known BotDeliveryChoices entry is supplied. These choices are replay inputs, not predictions of the hotel's choice. Fork preserves pending delivery work; copying Room alone does not.",
         "Text placeholders use exact case-sensitive $(name) tokens. Capture includes localized Furniture.Name; do not substitute a class identifier for a missing name. Empty sources keep a single-value token literal but produce an empty string in multiple-value mode. Selected holders without a variable render null. Multiple-source single output is uncertain; multiple-value order is also uncertain because it changed after room entry. Supply resolved Variable.Text mappings: empty means known no entries, null means unknown metadata. Unmapped values fall back to numeric text, mapped empty strings stay empty, and acyclic nested local tokens resolve. Global placeholders require separate conformance.",
         "TimeMatches and DateMatches require an explicit Epoch and evaluate Epoch plus virtual event time in their configured timezone. For an empty timezone, supply the known hotel CalendarTimezone; never use the workstation timezone implicitly. Enabled second, minute, hour and day ranges are inclusive and can wrap. Date masks are mandatory selections: zero weekdays or zero months match nothing. Inverted year settings are normalized by the server; use inspected readback. Recorded named zones include UTC, Europe/Berlin, America/New_York and Asia/Kathmandu; DST transitions still need live conformance.",
         "Fork before testing an alternative; assert expected states and invariants. Delete unused sessions.",
         "Derived variables require their parent and add-on metadata. Actual server IDs can be decoded; planned variables can supply Derived explicitly. Variable assertions resolve these computed outputs, and *_variable_present assertions distinguish an absent flag from a zero number. Level is_maxed is absent below the last level and present with value -1 at the cap. Time utility calendar fields require the calibrated TimeUtilities timezone and week profile. Management queries may clamp large values: establish full 64-bit input separately before replaying it.",
         $"Calendar conversion uses bundled timezone rules ({NodaTime.DateTimeZoneProviders.Tzdb.VersionId}) so host operating-system history does not change replay results. Time utility milliseconds through hours measure elapsed time; days, weeks and months count complete local-calendar units from Unix epoch. Value-source writes preserve smaller components. Creation/update sources read Epoch plus the parent's relative CreatedAt/UpdatedAt, require an explicit Epoch, and are read-only. Their index-zero timestamp alias exists even with an empty component mask. Gap/overlap writes and additional derived-change event behavior remain separate conformance targets.",
         "Result.VariableFx projects numeric displays, ranges, renderer configuration and eligible audience user IDs. Views adds per-viewer visibility, hover and expiry. Initial values are room-entry initialization and do not open timed windows. Supply Room.FxPresentation to restore remaining windows and pointer state, or inject a Hover event with its Hover payload. Matching created/increased/decreased/unchanged updates replace the deadline; equality at expiry is hidden. Hover is local input and adds no Wired work units. EntityId and ViewerId are stable user IDs; native user FX packets use avatar Index. Segment counts 1..100 require an effective renderer of 2, 4 or 13; level style 0 uses its sub-renderer. Standard number-display icons and left/right/double alignment are projected. Campaign icon availability remains guarded.",
         "Immediate FX changes are published at the end of the current virtual instant. One live immediate effect chain emitted only its final value, while delayed effects emitted separate updates. Batching across independent simultaneous roots still requires conformance. A paused result retains the last published FX value even if intermediate variables changed. Use Fork to preserve queued work and publication state; export Room.FxPresentation only after the current instant has settled.",
         "Team placement requires Room.TeamScores for teams 1 through 4. Ties have distinct places and retain the prior ranking when scores change. Supply Room.TeamOrder for tied initial scores; current scores alone cannot reconstruct historical tie order. Distinct scores establish their unique ranking. A cached room capture does not infer game scores or ranking history. Fork and room export preserve known order.",
         "MovementAnimation effects with a verified curve include Movement: native source/destination, duration, direction and nullable curve/jump fields. Zero curve values clear those fields. For the tested furniture animations, NoMoveAnimation preserves the logical destination but suppresses the movement packet and its curve payload; Value is zero and Movement is null. Native client movement events can report such a move as Update instead of Wired. A carried human instead retains an Avatar movement payload with duration zero and its support/posture-based heights. Variable curve operands are not limited to the constant slider range. Intermediate rendered frames are not simulated.",
         "Projectile internal variables use furniture.Projectile independently of the furniture's immediate logical destination. Supply Room.ProjectileTiming with the update interval and phase for asynchronous cell transitions. The observed server cadence is about 50 ms; selecting a hypothetical phase is not a server timing measurement. Crossed-cell midpoints determine sampled altitude and curve height. Height samples and their centimetre conversion preserve the observed float rounding stages. Human contacts compare the unrounded sample with user Z through Z+2.1; ActionHeight does not shift the tested posture bounds. During a flat walking step, NextStep is the server contact tile. To replay raw UserUpdate records as Walk events, use MovingTo when present and Location otherwise; Walk inputs clear stale NextStep metadata. This supplies observed logical positions and does not predict a human navigation path. UserHits and FurnitureHits both retain repeated IDs for contacts on different cells; entering a checked cell later does not cause a new contact. The collision-variable callback selects the projectile and no triggering user and does not emit AvatarCaught in these cases. Snapshot export rebases elapsed time and phase; Fork retains queued work. Same-modifier replacement supersedes pending flight callbacks. Changing the modifier mask alone does not rewrite a running flight. Bots, pets, walking height changes, overlapping users and overlapping furniture collisions remain guarded.",
         "Room capture separates configurable Wired from game furniture using catalog categories, numeric configuration codes and known form families. Unknown Wired categories and known form families remain incomplete even when their code has no implementation. The legacy color tile has seven states; the team color tile has ten. A switch producing no state update does not establish a one-state cycle. Other uncaptured state counts still require explicit metadata.",
         "Visuals projects each current human viewer's furniture/avatar transparency and highlight state from client-source rules. It is separate from collision, Hidden, geometry and furniture State. Supply Room.Visuals to reproduce preexisting fades or pulse phase; an absent visual entry starts from the client's initial display state. Selected audience uses user slot 0; an avatar visual target uses user slot 1. Effects retain the projected visual command. The restricted visual Wired types have no live-server conformance; late-viewer state and server override policy remain open.",
         "Room.Links lists each WIRED Link with its live configuration (network ID, \"0\" when disconnected, and receive-local), as read with GetWiredLinkConfiguration. A receive-local Link receives its own sends in a new execution without a triggering user; other Links in the room never receive them. Connected Links emit link_send payloads; deliver one to another room simulation with ReceiveAt only while that room is loaded. Messages are cut to 1000 characters. Missing or nonnumeric dataframe values become \"null\" or -1 with a level 3 room log. Typed numeric cells preserve signed 64-bit values. RoomInitialized is an explicit lifecycle input, separate from Start or Enter.",
         "Keep uncertain or unsupported behavior as an open requirement for later controlled server conformance tests."], WiredSimulationRules.Coverage());

    private static bool requires_wired_form(string identifier, FurniInfo? info)
    {
        bool wired_name = identifier.StartsWith("wf_", StringComparison.OrdinalIgnoreCase);
        if (info is null) return wired_name;
        if (info.Category.StartsWith("wired", StringComparison.OrdinalIgnoreCase) &&
            !info.Category.Equals("wired_add_on", StringComparison.OrdinalIgnoreCase)) return true;
        if (!wired_name) return false;
        if (int.TryParse(info.CustomParams, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out _)) return true;
        return wired_form_prefixes.Any(prefix => identifier.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private WiredSimulationRoom capture(WiredSimulationCaptureRequest request) => game.Room.Capture(room =>
    {
        if (!room.IsInRoom || room.FloorPlan is not { } floor) throw new InvalidOperationException("A loaded room floor is required.");
        var missing = new List<string> { "Hidden or server-only furniture may be absent; this is a cached client snapshot.",
            "Existing per-viewer Wired transparency, highlight and pulse phase are not present in the cached room snapshot; provide Visuals before reproducing an existing display.",
            "Timed FX windows and hover targets require explicit FxPresentation metadata; current variable values do not reveal earlier matching updates or pointer state.",
            "ClickSettings belongs to each recipient client; another user's current click preferences cannot be inferred from furniture or position.",
            "State counts, state-dependent collisions, user game state and holder variable values require explicit metadata. Supported saved furniture snapshots are decoded from supplied forms; missing or invalid snapshot data remains unknown.",
            "Referenced variable definitions are resolved through the native room-variable query when omitted from a capture request. Supplied definitions take precedence; stored holder values still require explicit capture.",
            "Projectile rotation requires supported furniture Directions when the kind's direction metadata has not been established.",
            "An active projectile's path, elapsed time, collision history and update phase are not recoverable from the furniture's logical destination. Supply Projectile and ProjectileTiming metadata when continuing an existing flight.",
            "Clock elapsed half seconds, initialization/running status and tick phase are not recoverable from visible integer furniture state; supply explicit clock metadata.",
            "Room timer value, next tick phase and pending reset state require explicit Timer metadata; visible furniture does not reveal them.",
            "Wired game phase, source game counter and per-game score allowance history require explicit Game metadata; a running counter does not prove the game is active.",
            "Pending classic teleport arrivals and temporary effect restoration are not recoverable from avatar position alone; capture an idle actor and supply calibrated TeleportTimings.",
            "Server freeze state, prior effect and cancel-on-teleport policy require explicit Frozen and Freeze metadata; do not infer them solely from a visible effect.",
            "Active bot following, item delivery, walking goals and pending bot callbacks are not recoverable from position or a held item alone; supply known navigation state or capture an idle bot.",
            "An avatar's prior Wired contact furniture can differ from its present support after a height-only change. Native geometry readouts do not recover this history; supply known User.WiredContacts for that prepared state.",
            "Ordinary furniture packets do not identify item origin reliably; supply Temporary and IntrinsicType from known creation history or an explicit @type object-variable query. Class availability in BC does not establish how that instance was placed." };
        var wired_state = game.Wired.Snapshot;
        var items = room.FloorItems.Select(item =>
        {
            var info = game.GameData.Furni?.GetInfo(ItemType.Floor, item.Kind);
            if (info is null) missing.Add("Furniture metadata missing: " + item.Id);
            IReadOnlyDictionary<int, WiredSimulationFurnitureState>? states = item.Identifier is "exe_gate" or "prison_gate"
                ? new Dictionary<int, WiredSimulationFurnitureState> { [0] = new(false, 0), [1] = new(true, 0) }
                : null;
            int? state_count = states?.Count ?? (item.Identifier switch { "wf_colortile" => 7, "wf_teamcolortile" => 10, "wf_fx_firegate" or "wf_antenna1" or "wf_antenna2" => 2, "bc_alpha1_alpha_recolorable" => 26, _ => (int?)null });
            return new WiredSimulationFurniture((long)item.Id, item.Id.ToString(), item.Identifier ?? info?.Identifier ?? "", item.Kind,
                item.X, item.Y, item.Z, item.Direction, item.State, item.SizeX, item.SizeZ, item.Height,
                StateCount: state_count, Walkable: info?.IsWalkable ?? false, Stackable: info?.CanPutStuffOn ?? false, Hidden: item.IsHidden,
                States: states, Stance: info?.CanSitOn == true ? AvatarStance.Sit : info?.CanLayOn == true ? AvatarStance.Lay : AvatarStance.Stand,
                IntrinsicState: item.Identifier is "wf_antenna1" or "wf_antenna2" ? 0 : null, Name: info?.Name is { Length: > 0 } name ? name : null,
                Directions: item.Identifier switch { "duck" or "lc_desk" => [0, 2, 4, 6], "lc_table" => [0, 2], "wf_colortile" => [0], _ => null },
                Chest: chest_contents(item, wired_state), RecolorRgb: item.RecolorRgb, RecolorPalette: recolor_palette(item.Identifier),
                Contract: wired_state.Contract.Contents is { } contract && contract.ContractId == (int)item.Id
                    ? WiredManager.SnapshotOf(contract) : null,
                RoomLink: item.Identifier == "wf_room_linker" && item.Extra > 0 ? new(item.Extra) : null);
        }).ToArray();
        var users = room.Avatars.Select(user => new WiredSimulationUser((long)user.Id, user.Name, user.X, user.Y,
            user.Location.Z, user.Direction, Effect: user.Effect, HandItem: user.HandItem, Action: -1,
            IsBot: user is Bot, IsPet: user is Pet, Figure: user.Figure, Stance: user.CurrentUpdate?.Stance ?? AvatarStance.Stand,
            ActionHeight: user.CurrentUpdate?.ActionHeight, NextStep: user.MovingTo, Index: user.Index,
            ClickSettings: user.Id == room.Self?.Id ? game.Wired.Snapshot.ClickSettings : null,
            Trade: user.Id == room.Self?.Id ? capture_trade(wired_state.Trade) : null,
            Activity: new(Dance: user.Dance))).ToArray();
        var nodes = new List<WiredSimulationNode>();
        if (request.Forms is { Count: > 0 } && !game.Wired.Snapshot.CanRead)
            throw new InvalidOperationException("Current server-granted Wired read permission is required for captured forms.");
        foreach (var view in request.Forms ?? [])
        {
            if (view.Generation != game.Wired.Snapshot.Generation) throw new ArgumentException("A supplied form belongs to another Wired generation.");
            if (!items.Any(item => item.Id == (long)view.Configuration.Id)) throw new ArgumentException("A supplied form is absent from the room.");
            var node = WiredSimulationNode.From(view.CreateForm());
            if (WiredSimulationSnapshots.supports(node))
            {
                try
                {
                    if (string.IsNullOrEmpty(view.Configuration.StringParam) && view.Configuration.StuffIds.Count > 0)
                        throw new FormatException("Selected furniture has no supplied saved snapshot.");
                    node = node with { SavedSnapshot = WiredSimulationSnapshots.parse(view.Configuration.StringParam) };
                }
                catch (FormatException error)
                {
                    missing.Add("Saved snapshot unavailable for " + node.FurnitureId + ": " + error.Message);
                }
            }
            nodes.Add(node);
        }
        var wired_items = items.Where(item => requires_wired_form(item.ClassName,
            game.GameData.Furni?.GetInfo(ItemType.Floor, item.Kind))).ToArray();
        var node_ids = nodes.Select(node => node.FurnitureId).ToHashSet();
        foreach (var item in wired_items.Where(item => !node_ids.Contains(item.Id))) missing.Add("Wired form not supplied: " + item.Id);
        double[] map = new double[floor.Width * floor.Length];
        for (int y = 0; y < floor.Length; y++)
            for (int x = 0; x < floor.Width; x++) map[y * floor.Width + x] = floor[x, y];
        var definitions = (request.Variables ?? []).ToDictionary(variable => variable.Id, StringComparer.Ordinal);
        foreach (var view in request.Forms ?? [])
        {
            foreach (var entry in view.Configuration.Context)
                if (entry.Variable is { } variable && view.Configuration.Kind == WiredConfigurationKind.Variable &&
                    definitions.TryGetValue(variable.VariableId, out var definition))
                    definitions[variable.VariableId] = definition with
                    {
                        DefinitionFurnitureId = (long)view.Configuration.Id,
                        TextFallback = variable.TextConnectorFallback,
                        Reference = view.CreateForm() is Qx.Model.Wired.WiredEchoVariableForm echo ? echo.ReferenceVariable : definition.Reference
                    };
            if (view.CreateForm() is WiredEchoVariableForm echo_form)
            {
                var matching = definitions.Values.Where(variable => variable.Availability == 21 && variable.Name == echo_form.Name).ToArray();
                if (matching.Length == 1)
                    definitions[matching[0].Id] = matching[0] with { DefinitionFurnitureId = (long)view.Configuration.Id, Reference = echo_form.ReferenceVariable };
            }
            if (view.CreateForm() is WiredContextVariableForm context_form)
            {
                var matching = definitions.Values.Where(variable => variable.Domain == WiredSourceDomain.Context && variable.Name == context_form.Name).ToArray();
                if (matching.Length == 1)
                    definitions[matching[0].Id] = matching[0] with { DefinitionFurnitureId = (long)view.Configuration.Id };
                else missing.Add("Context variable definition is missing or ambiguous: " + context_form.Name);
            }
            if (view.CreateForm() is WiredPointerVariableForm pointer_form)
            {
                var matching = definitions.Values.Where(variable => variable.Name == pointer_form.Name &&
                    variable.Domain == pointer_form.Target && variable.RequiresExecutionContext != false).ToArray();
                if (matching.Length == 1)
                    definitions[matching[0].Id] = matching[0] with { DefinitionFurnitureId = (long)view.Configuration.Id,
                        PointerAccessor = pointer_form.AccessorVariable, RequiresExecutionContext = true };
                else missing.Add("Pointer variable definition is missing or ambiguous: " + pointer_form.Name);
            }
            if (view.CreateForm() is WiredReferenceVariableForm reference_form)
                bind_reference(view, reference_form, definitions, missing);
        }
        foreach (var view in request.Forms ?? [])
        {
            if (view.CreateForm() is not WiredVariableLeaderboardAddonForm board) continue;
            int? overlay = view.Configuration.Context.LastOrDefault(entry => entry.VariableLeaderboard is not null)?.VariableLeaderboard?.OverlayInstanceId;
            var descriptor = wired_state.Leaderboards.FirstOrDefault(value => value.OverlayInstanceId == overlay);
            var overrides = Enumerable.Range(1, board.DisplayOverrideCount).Select(index => board.GetField("display_variable_" + index).Text).ToHashSet();
            foreach (var choice in descriptor?.Choices ?? [])
                if (!overrides.Contains(choice.VariableId) && definitions.TryGetValue(choice.VariableId, out var definition) && definition.DisplayName is null)
                    definitions[choice.VariableId] = definition with { DisplayName = choice.DisplayName };
        }
        var shared_placeholders = (request.Forms ?? []).SelectMany(view => view.Configuration.Context)
            .SelectMany(context => context.SharedPlaceholders ?? [])
            .GroupBy(placeholder => (placeholder.RoomId, placeholder.PlaceholderName))
            .Select(group => new WiredSimulationSharedPlaceholder((long)group.Key.RoomId, group.Key.PlaceholderName,
                RoomName: group.First().RoomName)).ToArray();
        if (nodes.Any(node => node.Name == "GlobalPlaceholder" && node.Number("mode") == 1 && node.Number("room_id") != (long)(room.Data?.Id ?? 0)))
            missing.Add("Shared global placeholder values are not present in the room's reference list; supply their explicit SharedPlaceholders values.");
        var captured = new WiredSimulationRoom((long)(room.Data?.Id ?? 0), (long)(room.Data?.OwnerId ?? 0), floor.Width, floor.Length,
            items, users, nodes, definitions.Values.ToArray(), Floor: map, Epoch: DateTimeOffset.UtcNow,
            Source: "cached room generation " + room.Generation, Incomplete: missing, GroupId: (long)room.GroupId,
            SharedPlaceholders: shared_placeholders);
        return Engine.CopyRoom(attach_object_values(captured, request.ObjectValues, wired_state.Generation, wired_state.CanRead));
    });

    private static WiredSimulationChest? chest_contents(FloorItem item, WiredSnapshot state)
    {
        string name = item.Identifier ?? "";
        bool coins = name.StartsWith("wf_storage_coins", StringComparison.OrdinalIgnoreCase);
        if (!coins && !name.StartsWith("wf_storage_furni", StringComparison.OrdinalIgnoreCase)) return null;
        var contents = state.Chests.FirstOrDefault(chest => chest.ChestId == item.Id);
        bool? flag(string key) => item.Data is MapData data && data.Entries.TryGetValue(key, out string? value)
            ? value == "1" ? true : value == "0" ? false : null : null;
        int? capacity = item.Data is MapData map && map.Entries.TryGetValue("capacity", out string? text) &&
            int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int amount)
            ? amount : null;
        return new(coins, contents?.Coins, contents?.Items, contents?.ItemsComplete ?? false,
            chest_preview(item), flag("is_wired_enabled"), flag("locked"), capacity);
    }

    private static WiredSimulationTrade? capture_trade(WiredTradeSnapshot trade) => trade.Status switch
    {
        WiredTradeStatus.None or WiredTradeStatus.Cancelled or WiredTradeStatus.Completed => new(false),
        WiredTradeStatus.Initiated or WiredTradeStatus.Active when trade.Initiation?.Requirement is { Type: 0 or 1, Rules: null } => new(true, ChestDeposit: true),
        _ => null
    };

    private static IReadOnlyList<Qx.Model.Wired.ChestItemType>? chest_preview(FloorItem item)
    {
        if (item.Data is not MapData data || !data.Entries.TryGetValue("visuals", out string? text)) return null;
        if (text.Length == 0) return [];
        var preview = new List<Qx.Model.Wired.ChestItemType>();
        foreach (string entry in text.Split(';'))
        {
            string[] fields = entry.Split(',');
            if (fields.Length != 2 || !bool.TryParse(fields[0], out bool wall) ||
                !int.TryParse(fields[1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int kind) || kind <= 0) return null;
            preview.Add(new(wall, kind, ""));
        }
        return preview;
    }

    private IReadOnlyList<int>? recolor_palette(string? name)
    {
        var settings = game.GameData.Variables;
        if (name is null || settings is null || !settings.TryGetValue("recolorable_bc.palette_to_furni", out string? groups)) return null;
        foreach (string group in groups.Split(';'))
        {
            string[] parts = group.Split('=', 2);
            if (parts.Length != 2 || !parts[1].Split(',').Contains(name, StringComparer.Ordinal)) continue;
            if (!settings.TryGetValue("recolorable_bc.palette." + parts[0], out string? colors)) return null;
            var palette = new List<int>();
            foreach (string text in colors.Split(','))
            {
                if (!int.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int color) || color is < 0 or > 0xffffff) return null;
                palette.Add(color);
            }
            return palette;
        }
        return null;
    }
}
