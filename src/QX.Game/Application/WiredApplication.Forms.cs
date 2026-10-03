using Qx.Game.Protocol;
using Qx.Model.Wired;

namespace Qx.Game.Application;

internal sealed partial class WiredApplication
{
    private ValueTask<IReadOnlyList<WiredFormDefinition>> list_form_definitions(
        WiredFormDefinitionsRequest request, CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        IReadOnlyList<WiredFormDefinition> result = Array.AsReadOnly(WiredFormRegistry.Definitions
            .Where(form => (request.Category is null || form.Category == request.Category) &&
                (request.Code is null || form.Code == request.Code || form.Aliases.Contains(request.Code.Value))).ToArray());
        return ValueTask.FromResult(result);
    }

    private ValueTask<IReadOnlyList<WiredFxStyleDefinition>> list_fx_styles(
        WiredFxStylesRequest request, CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellation_token.ThrowIfCancellationRequested();
        IReadOnlyList<WiredFxStyleDefinition> result = Array.AsReadOnly(WiredFxStyles.All
            .Where(style => request.Category is null || style.Category == request.Category).ToArray());
        return ValueTask.FromResult(result);
    }

    private async ValueTask<WiredFormView> get_form(
        WiredConfigurationGetRequest request, CancellationToken cancellation_token)
    {
        WiredConfigurationSnapshot configuration = await GetConfiguration(request, cancellation_token).ConfigureAwait(false);
        WiredSnapshot state = wired.Snapshot;
        if (!ReferenceEquals(state.Configuration, configuration))
            throw new InvalidOperationException("The configuration changed before it could be reviewed. Read it again.");
        WiredForm form = WiredFormView.ReadForm(configuration);
        return new(state.Generation, state.Revision, configuration, form.Definition,
            Array.AsReadOnly(form.Definition.Fields.Select(field => new WiredFormFieldView(field,
                form.GetField(field.Name), form.GetDefaultField(field.Name),
                WiredFormChoices.Get(form.Definition, field.Name), WiredFormChoices.IsFlags(form.Definition, field.Name))).ToArray()));
    }

    private ValueTask<WiredConfigurationSaveResult> save_form(
        WiredFormSaveRequest request, CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Patch);
        WiredSnapshot state = validate_form_review(request);
        WiredForm form = WiredFormView.ReadForm(state.Configuration!);
        request.Patch.Apply(form);
        Action validation = () => validate_form_review(request);
        return form.ToUpdate() switch
        {
            UpdateTrigger update => SaveConfiguration(update, MessageContracts.Wired.Configuration.TriggerUpdate, request.TimeoutMilliseconds, cancellation_token, validation),
            UpdateAction update => SaveConfiguration(update, MessageContracts.Wired.Configuration.ActionUpdate, request.TimeoutMilliseconds, cancellation_token, validation),
            UpdateCondition update => SaveConfiguration(update, MessageContracts.Wired.Configuration.ConditionUpdate, request.TimeoutMilliseconds, cancellation_token, validation),
            UpdateSelector update => SaveConfiguration(update, MessageContracts.Wired.Configuration.SelectorUpdate, request.TimeoutMilliseconds, cancellation_token, validation),
            UpdateAddon update => SaveConfiguration(update, MessageContracts.Wired.Configuration.AddonUpdate, request.TimeoutMilliseconds, cancellation_token, validation),
            UpdateVariable update => SaveConfiguration(update, MessageContracts.Wired.Configuration.VariableUpdate, request.TimeoutMilliseconds, cancellation_token, validation),
            _ => throw new InvalidOperationException("Unsupported form category.")
        };
    }

    private WiredSnapshot validate_form_review(WiredFormSaveRequest request)
    {
        WiredSnapshot state = wired.Snapshot;
        if (state.Generation != request.ExpectedGeneration || state.Revision != request.ExpectedRevision ||
            state.Configuration?.Id != request.FurniId)
            throw new InvalidOperationException("The reviewed Wired configuration is stale. Read it again before saving.");
        return state;
    }
}
