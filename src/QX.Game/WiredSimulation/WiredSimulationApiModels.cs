using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public enum WiredSimulationApiOperation
{
    ReadValue, ReplaceValue, PatchValue, DeleteValue, ReadProfile, PatchProfile, Batch, BulkDelete
}

public sealed record WiredSimulationApiAddress(string Kind, long ExternalId, long EntityId);

public sealed record WiredSimulationApiAccess(bool Enabled = false, bool Read = false, bool Write = false,
    IReadOnlyList<WiredSimulationApiAddress>? Addresses = null, bool AllowMassDeletion = false,
    bool AllHoldersKnown = false, bool? BulkDeleteNotifications = null);

public sealed record WiredSimulationApiRequest(WiredSimulationApiOperation Operation, WiredSourceDomain Domain = WiredSourceDomain.Global,
    string? Variable = null, string? TargetKind = null, long? EntityId = null, long? Value = null,
    IReadOnlyDictionary<string, long?>? Values = null, IReadOnlyList<WiredSimulationApiRequest>? Operations = null,
    string? Id = null, IReadOnlyList<string>? Variables = null);

public sealed record WiredSimulationApiResponse(int Status, long At, string? Id = null, string? Error = null,
    IReadOnlyDictionary<string, WiredSimulationValue>? Values = null,
    IReadOnlyList<WiredSimulationApiResponse>? Results = null);
