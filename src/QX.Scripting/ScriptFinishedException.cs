namespace Qx.Scripting;

/// <summary>Thrown when a script ends its run early through <see cref="ScriptGlobals.Finish"/>.</summary>
/// <remarks>
/// The host treats it as a normal finish rather than a failure. It derives from
/// <see cref="OperationCanceledException"/>, so a <c>catch (Exception)</c> in the script swallows it.
/// </remarks>
public sealed class ScriptFinishedException : OperationCanceledException;
