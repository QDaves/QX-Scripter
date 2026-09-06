using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class WalletApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.WalletState,
        "Wallet state",
        "Reads credits and a bounded deterministic activity-point page for the active session.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(WalletStateRequest),
        typeof(WalletStateView),
        StateParameters(),
        state_effects:
        [new(ApplicationStateKey.WalletLoaded, ApplicationStateEffectKind.Reads)],
        messages: ObservedMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.WalletRefresh,
        "Refresh wallet",
        "Refreshes credits and returns the latest observed activity-point balances for the active session.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(WalletRefreshRequest),
        typeof(WalletStateView),
        [PointLimitParameter(), TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.WalletLoaded, ApplicationStateEffectKind.Changes)],
        [
            new(MessageKeys.Wallet.CreditsRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Wallet.CreditsBalance, Direction.In, ApplicationMessageRole.Observe),
            new(MessageKeys.Wallet.ActivityPoints, Direction.In, ApplicationMessageRole.Observe),
            new(
                MessageKeys.Wallet.ActivityPointUpdated,
                Direction.In,
                ApplicationMessageRole.Observe,
                false)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.WalletChanged,
        "Wallet changed",
        "Publishes bounded credit, activity-point, delta and reset envelopes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(WalletChanged),
        state_effects:
        [new(ApplicationStateKey.WalletLoaded, ApplicationStateEffectKind.Changes)],
        messages: ObservedMessages());

    private static IReadOnlyList<ApplicationParameterDescriptor> StateParameters() =>
    [
        new(
            "point_offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the selected activity-point snapshot.",
            new(Minimum: 0)),
        PointLimitParameter(),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Activity-point snapshot revision required for continuation pages.",
            new(Minimum: 1)),
        new(
            "point_type",
            typeof(int?),
            false,
            null,
            "Optional exact activity-point type.")
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        new(MessageKeys.Wallet.CreditsBalance, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Wallet.ActivityPoints, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Wallet.ActivityPointUpdated, Direction.In, ApplicationMessageRole.Observe)
    ];

    private static ApplicationParameterDescriptor PointLimitParameter() => new(
        "point_limit",
        typeof(int),
        false,
        100,
        "Maximum activity-point balances returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total time for the credit balance response.",
        new(Minimum: 1, Maximum: 120000));
}
