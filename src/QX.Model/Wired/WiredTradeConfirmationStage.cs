namespace Qx.Model.Wired;

/// <summary>Specifies the two separate stages of accepting a Wired trade.</summary>
public enum WiredTradeConfirmationStage
{
    /// <summary>Accepts the current offer and begins the confirmation countdown; encoded as false.</summary>
    Accept,
    /// <summary>Finalizes the accepted offer after the countdown; encoded as true.</summary>
    Confirm
}
