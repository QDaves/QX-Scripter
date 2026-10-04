using Qx.Model.Wired;

namespace Qx.Game.Application;

/// <summary>Requests both confirmation stages for a reviewed, already acceptable Wired trade.</summary>
/// <param name="ExpectedGeneration">The generation of the reviewed Wired state.</param>
/// <param name="ExpectedRevision">The revision of the reviewed Wired state.</param>
/// <param name="TimeoutMilliseconds">The total timeout, from 1 to 120000 milliseconds.</param>
public sealed record WiredTradeCompleteRequest(
    long ExpectedGeneration,
    long ExpectedRevision,
    int TimeoutMilliseconds = 30000);

/// <summary>Represents the outcome of a complete-trade operation.</summary>
/// <param name="Success">Whether the server reported completion after final confirmation.</param>
/// <param name="Failure">The failure reason, or an empty string on success.</param>
/// <param name="FailureTypeId">The server cancellation or failure code, when supplied.</param>
/// <param name="Generation">The Wired state generation at the result.</param>
/// <param name="Revision">The Wired state revision at the result.</param>
public sealed record WiredTradeCompleteResult(
    bool Success,
    string Failure,
    int? FailureTypeId,
    long Generation,
    long Revision);

/// <summary>Requests one explicit stage without waiting for the trade outcome.</summary>
/// <param name="Stage">Initial acceptance or final confirmation.</param>
public sealed record WiredTradeStageRequest(WiredTradeConfirmationStage Stage);
