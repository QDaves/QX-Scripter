using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>Reads form definitions and practical usage knowledge with evidence levels, without sending to the hotel.</summary>
    /// <param name="category">An optional category.</param>
    /// <param name="code">An optional primary or alias code.</param>
    /// <returns>The matching typed form definitions.</returns>
    public IReadOnlyList<WiredFormDefinition> GetWiredFormDefinitions(WiredFormCategory? category = null, int? code = null) =>
        _application.Invoke<WiredFormDefinitionsRequest, IReadOnlyList<WiredFormDefinition>>(
            ApplicationMemberIds.WiredFormDefinitions, new(category, code), Ct);

    /// <summary>Reads verified FX styles and their allowed visualization choices.</summary>
    /// <param name="category">An optional category.</param>
    /// <returns>The matching style definitions.</returns>
    public IReadOnlyList<WiredFxStyleDefinition> GetWiredFxStyles(WiredFxCategory? category = null) =>
        _application.Invoke<WiredFxStylesRequest, IReadOnlyList<WiredFxStyleDefinition>>(
            ApplicationMemberIds.WiredFxStyles, new(category), Ct);

    /// <summary>Opens a Wired configuration and returns named fields with a review token.</summary>
    /// <param name="furniId">The Wired furniture ID.</param>
    /// <param name="timeoutMs">The timeout including the opening queue.</param>
    /// <returns>The reviewed configuration; CreateForm returns its concrete typed editor.</returns>
    public Task<WiredFormView> GetWiredForm(Id furniId, int timeoutMs = 10000) =>
        wired_call<WiredConfigurationGetRequest, WiredFormView>(ApplicationMemberIds.WiredFormGet, new(furniId, timeoutMs));

    /// <summary>Saves only edits made to a typed form, rejecting a stale review before sending.</summary>
    /// <param name="review">The original form read.</param>
    /// <param name="form">The edited typed form.</param>
    /// <param name="timeoutMs">The timeout including the save queue.</param>
    /// <returns>The hotel save or validation result.</returns>
    public Task<WiredConfigurationSaveResult> SaveWiredForm(WiredFormView review, WiredForm form, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(review);
        return SaveWiredForm(review, review.CreatePatch(form), timeoutMs);
    }

    /// <summary>Applies a named patch to the reviewed configuration and awaits the hotel result.</summary>
    /// <param name="review">The original form read.</param>
    /// <param name="patch">The named changes.</param>
    /// <param name="timeoutMs">The timeout including the save queue.</param>
    /// <returns>The hotel save or validation result.</returns>
    public Task<WiredConfigurationSaveResult> SaveWiredForm(WiredFormView review, WiredFormPatch patch, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(review);
        return wired_call<WiredFormSaveRequest, WiredConfigurationSaveResult>(ApplicationMemberIds.WiredFormSave,
            new(review.Configuration.Id, review.Generation, review.Revision, patch, timeoutMs));
    }
}
