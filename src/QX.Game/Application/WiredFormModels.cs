using Qx.Model.Wired;

namespace Qx.Game.Application;

/// <summary>A named field with its received value, server default and verified options.</summary>
/// <param name="Definition">The field metadata.</param>
/// <param name="Value">The current value.</param>
/// <param name="Default">The server default when present.</param>
/// <param name="Choices">Named integer options.</param>
/// <param name="Flags">Whether choices can be combined as flags.</param>
public sealed record WiredFormFieldView(WiredFormFieldDefinition Definition, WiredFormValue Value,
    WiredFormValue? Default, IReadOnlyList<WiredFormChoice> Choices, bool Flags);

/// <summary>A furniture source selection edit.</summary>
/// <param name="Selection">The zero-based selection index.</param>
/// <param name="Source">The new source.</param>
public sealed record WiredFurnitureSourceEdit(int Selection, WiredFurnitureSource Source);

/// <summary>A user source selection edit.</summary>
/// <param name="Selection">The zero-based selection index.</param>
/// <param name="Source">The new source.</param>
public sealed record WiredUserSourceEdit(int Selection, WiredUserSource Source);

/// <summary>A named patch; omitted controls retain their received values.</summary>
/// <param name="Fields">Named field edits in application order.</param>
/// <param name="Furniture">Replacement primary selection, or null to preserve.</param>
/// <param name="SecondaryFurniture">Replacement secondary selection, or null to preserve.</param>
/// <param name="FurnitureSources">Indexed furniture source edits.</param>
/// <param name="UserSources">Indexed user source edits.</param>
/// <param name="ActionDelay">Action delay in pulses, or null to preserve.</param>
/// <param name="ConditionQuantifier">Condition quantifier, or null to preserve.</param>
/// <param name="SelectorFilter">Selector filter flag, or null to preserve.</param>
/// <param name="SelectorInvert">Selector inversion, or null to preserve.</param>
public sealed record WiredFormPatch(
    IReadOnlyList<WiredFormEdit>? Fields = null,
    IReadOnlyList<Id>? Furniture = null,
    IReadOnlyList<Id>? SecondaryFurniture = null,
    IReadOnlyList<WiredFurnitureSourceEdit>? FurnitureSources = null,
    IReadOnlyList<WiredUserSourceEdit>? UserSources = null,
    int? ActionDelay = null, int? ConditionQuantifier = null,
    bool? SelectorFilter = null, bool? SelectorInvert = null)
{
    internal void Apply(WiredForm form)
    {
        form.SetFields(Fields ?? []);
        if (Furniture is not null) form.SelectFurniture(Furniture);
        if (SecondaryFurniture is not null) form.SelectFurniture(SecondaryFurniture, true);
        foreach (WiredFurnitureSourceEdit edit in FurnitureSources ?? [])
            form.SelectFurnitureSource(edit.Selection, edit.Source);
        foreach (WiredUserSourceEdit edit in UserSources ?? [])
            form.SelectUserSource(edit.Selection, edit.Source);
        if (ActionDelay is not null) form.ActionDelay = ActionDelay;
        if (ConditionQuantifier is not null) form.ConditionQuantifier = ConditionQuantifier;
        if (SelectorFilter is not null) form.SelectorFilter = SelectorFilter;
        if (SelectorInvert is not null) form.SelectorInvert = SelectorInvert;
    }
}

/// <summary>A configuration read together with its review token and named fields.</summary>
/// <param name="Generation">The reviewed Wired generation.</param>
/// <param name="Revision">The reviewed Wired revision.</param>
/// <param name="Configuration">The immutable original configuration.</param>
/// <param name="Definition">The resolved form definition.</param>
/// <param name="Fields">The named field values and choices.</param>
public sealed record WiredFormView(long Generation, long Revision, WiredConfigurationSnapshot Configuration,
    WiredFormDefinition Definition, IReadOnlyList<WiredFormFieldView> Fields)
{
    /// <summary>Creates an independent typed editor from the reviewed configuration.</summary>
    /// <returns>The concrete form, preserving untouched save fields.</returns>
    public WiredForm CreateForm() => ReadForm(Configuration);

    /// <summary>Builds a patch containing only changed named fields, sources and category controls.</summary>
    /// <param name="form">The edited form created from this review.</param>
    /// <returns>The patch that preserves all untouched wire values.</returns>
    public WiredFormPatch CreatePatch(WiredForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        WiredForm original = CreateForm();
        if (form.FurniId != original.FurniId || form.DefinitionCode != original.DefinitionCode ||
            form.Definition.Category != original.Definition.Category)
            throw new ArgumentException("The edited form does not match the reviewed configuration.", nameof(form));
        var fields = new List<WiredFormEdit>();
        foreach (WiredFormFieldDefinition field in original.Definition.Fields.Where(field => field.Writable))
        {
            WiredFormValue before = original.GetField(field.Name);
            WiredFormValue after = form.GetField(field.Name);
            bool equal = before.Integer == after.Integer && before.Boolean == after.Boolean && before.Text == after.Text &&
                (before.Tiles ?? []).SequenceEqual(after.Tiles ?? []) && (before.Rewards ?? []).SequenceEqual(after.Rewards ?? []);
            if (!equal) fields.Add(new(field.Name, after));
        }
        return new(fields.AsReadOnly(),
            original.SelectedFurniture.SequenceEqual(form.SelectedFurniture) ? null : form.SelectedFurniture,
            original.SecondaryFurniture.SequenceEqual(form.SecondaryFurniture) ? null : form.SecondaryFurniture,
            Array.AsReadOnly(form.FurnitureSources.Select((source, index) => new WiredFurnitureSourceEdit(index, source))
                .Where(edit => edit.Selection >= original.FurnitureSources.Count || original.FurnitureSources[edit.Selection] != edit.Source).ToArray()),
            Array.AsReadOnly(form.UserSources.Select((source, index) => new WiredUserSourceEdit(index, source))
                .Where(edit => edit.Selection >= original.UserSources.Count || original.UserSources[edit.Selection] != edit.Source).ToArray()),
            original.ActionDelay == form.ActionDelay ? null : form.ActionDelay,
            original.ConditionQuantifier == form.ConditionQuantifier ? null : form.ConditionQuantifier,
            original.SelectorFilter == form.SelectorFilter ? null : form.SelectorFilter,
            original.SelectorInvert == form.SelectorInvert ? null : form.SelectorInvert);
    }

    internal static WiredForm ReadForm(WiredConfigurationSnapshot value)
    {
        WiredConfig config = value.Kind switch
        {
            WiredConfigurationKind.Trigger => new WiredTriggerConfig(),
            WiredConfigurationKind.Action => new WiredActionConfig { DelayInPulses = value.DelayInPulses ?? 0 },
            WiredConfigurationKind.Condition => new WiredConditionConfig { QuantifierCode = value.QuantifierCode ?? 0 },
            WiredConfigurationKind.Selector => new WiredSelectorConfig { IsFilter = value.IsFilter ?? false, IsInvert = value.IsInvert ?? false },
            WiredConfigurationKind.Addon => new WiredAddonConfig(),
            WiredConfigurationKind.Variable => new WiredVariableConfig(),
            _ => throw new ArgumentException("Unknown Wired category.", nameof(value))
        };
        config.Id = value.Id;
        config.Code = value.Code;
        config.FurniLimit = value.FurniLimit;
        config.AllowWallFurni = value.AllowWallFurni;
        config.StuffTypeId = value.StuffTypeId;
        config.AdvancedMode = value.AdvancedMode;
        config.StuffIds = value.StuffIds;
        config.StuffIds2 = value.StuffIds2;
        config.IntParams = value.IntParams;
        config.StringParam = value.StringParam;
        config.VariableIds = value.VariableIds;
        config.FurniSourceTypes = value.FurniSourceTypes;
        config.UserSourceTypes = value.UserSourceTypes;
        config.DefaultIntParams = value.DefaultIntParams;
        config.InputSources = new(value.InputSources.AllowedFurniSources, value.InputSources.AllowedUserSources,
            value.InputSources.DefaultFurniSources, value.InputSources.DefaultUserSources);
        return WiredForm.Read(config);
    }
}

/// <summary>Saves a named patch only while the reviewed state remains current.</summary>
/// <param name="FurniId">The reviewed furniture identifier.</param>
/// <param name="ExpectedGeneration">The generation returned by the form read.</param>
/// <param name="ExpectedRevision">The revision returned by the form read.</param>
/// <param name="Patch">The named edits.</param>
/// <param name="TimeoutMilliseconds">The total save timeout including the queue.</param>
public sealed record WiredFormSaveRequest(Id FurniId, long ExpectedGeneration, long ExpectedRevision,
    WiredFormPatch Patch, int TimeoutMilliseconds = 10000);

/// <summary>Filters the local verified form catalog.</summary>
/// <param name="Category">An optional category filter.</param>
/// <param name="Code">An optional primary or alias code filter.</param>
public sealed record WiredFormDefinitionsRequest(WiredFormCategory? Category = null, int? Code = null);

/// <summary>Filters the local FX style catalog.</summary>
/// <param name="Category">An optional FX category.</param>
public sealed record WiredFxStylesRequest(WiredFxCategory? Category = null);
