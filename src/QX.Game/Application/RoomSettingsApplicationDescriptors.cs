using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class RoomSettingsApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.RoomSettingsState,
        "Room settings state",
        "Reads the bounded cached settings snapshot for one owned room.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(RoomSettingsStateRequest),
        typeof(RoomSettingsStateView),
        [RoomIdParameter()],
        state_effects:
        [new(ApplicationStateKey.RoomSettingsLoaded, ApplicationStateEffectKind.Reads)],
        messages: ObservedMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Get { get; } = new(
        ApplicationMemberIds.RoomSettingsGet,
        "Get room settings",
        "Loads the complete editable settings and read-only metadata for one owned room.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomSettingsGetRequest),
        typeof(RoomSettingsStateView),
        [
            RoomIdParameter(),
            TimeoutParameter(),
            SessionGenerationParameter(),
            RoomGenerationParameter()
        ],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.RoomSettingsLoaded, ApplicationStateEffectKind.Changes)],
        [
            new(MessageKeys.Room.Settings.Request, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Room.Settings.Snapshot, Direction.In, ApplicationMessageRole.Observe),
            new(MessageKeys.Room.Settings.RequestFailed, Direction.In, ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor Save { get; } = new(
        ApplicationMemberIds.RoomSettingsSave,
        "Save room settings",
        "Atomically replaces one owned room's editable settings and waits for the terminal server acknowledgement.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomSettingsSaveRequest),
        typeof(RoomSettingsSaveReceipt),
        [
            new("settings", typeof(RoomSettingsValues), true, null, "Complete writable settings block including mute, kick and ban policies."),
            new("password", typeof(string), false, "", "Door password; an empty value clears an existing password.", new(MaxUtf8Bytes: ushort.MaxValue)),
            TimeoutParameter(),
            SessionGenerationParameter(),
            RoomGenerationParameter(),
            new("expected_operation_revision", typeof(long?), false, null, "Optional room-operation revision returned by a prior settings read.", new(Minimum: 1)),
            new("expected_snapshot_revision", typeof(long?), false, null, "Optional settings snapshot revision returned by a prior settings read.", new(Minimum: 1))
        ],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.RoomSettingsLoaded, ApplicationStateEffectKind.Invalidates)],
        [
            new(MessageKeys.Room.Settings.Save, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Room.Settings.SaveSucceeded, Direction.In, ApplicationMessageRole.Observe),
            new(MessageKeys.Room.Settings.SaveFailed, Direction.In, ApplicationMessageRole.Observe)
        ],
        new(false, false, true, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.RoomSettingsChanged,
        "Room settings changed",
        "Publishes bounded room-settings refresh, rejection, invalidation, acknowledgement and lifecycle summaries.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomSettingsChanged),
        state_effects:
        [
            new(ApplicationStateKey.RoomSettingsLoaded, ApplicationStateEffectKind.Changes),
            new(ApplicationStateKey.RoomSettingsLoaded, ApplicationStateEffectKind.Invalidates)
        ],
        messages:
        [
            .. ObservedMessages(),
            new(MessageKeys.Room.Settings.Save, Direction.Out, ApplicationMessageRole.Observe)
        ]);

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        new(MessageKeys.Room.Settings.Snapshot, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Room.Settings.RequestFailed, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Room.Settings.SaveSucceeded, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Room.Settings.SaveFailed, Direction.In, ApplicationMessageRole.Observe)
    ];

    private static ApplicationParameterDescriptor RoomIdParameter() => new(
        "room_id",
        typeof(Id),
        true,
        null,
        "Positive owned room identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total wait including lane contention and any permitted retry.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor SessionGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApplicationParameterDescriptor RoomGenerationParameter() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional guard used only when the target is the currently active room.",
        new(Minimum: 0));
}
