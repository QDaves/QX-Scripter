namespace Qx.Model.Wired;

/// <summary>An editable typed view of a Wired configuration that preserves fields not edited by the caller.</summary>
public abstract class WiredForm
{
    private readonly WiredFormLayout layout;
    private readonly WiredConfigWrite update;
    private readonly InputSourcesConf sources;
    private readonly int[] defaults;

    internal WiredForm(WiredConfig configuration, WiredFormLayout layout)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.layout = layout;
        defaults = configuration.DefaultIntParams.ToArray();
        DefinitionCode = configuration.Code;
        FurnitureLimit = configuration.FurniLimit;
        AllowWallFurniture = configuration.AllowWallFurni;
        sources = new InputSourcesConf(
            Array.AsReadOnly(configuration.InputSources.AllowedFurniSources.Select(values => (IReadOnlyList<int>)Array.AsReadOnly(values.ToArray())).ToArray()),
            Array.AsReadOnly(configuration.InputSources.AllowedUserSources.Select(values => (IReadOnlyList<int>)Array.AsReadOnly(values.ToArray())).ToArray()),
            Array.AsReadOnly(configuration.InputSources.DefaultFurniSources.ToArray()),
            Array.AsReadOnly(configuration.InputSources.DefaultUserSources.ToArray()));
        update = configuration switch
        {
            WiredTriggerConfig => new UpdateTrigger(),
            WiredActionConfig action => new UpdateAction { Delay = action.DelayInPulses },
            WiredConditionConfig condition => new UpdateCondition { Quantifier = condition.QuantifierCode },
            WiredSelectorConfig selector => new UpdateSelector { IsFilter = selector.IsFilter, IsInvert = selector.IsInvert },
            WiredAddonConfig => new UpdateAddon(),
            WiredVariableConfig => new UpdateVariable(),
            _ => throw new ArgumentException("Unsupported Wired configuration category.", nameof(configuration))
        };
        update.FurniId = configuration.Id;
        update.IntParams = configuration.IntParams.ToArray();
        update.StringParam = configuration.StringParam;
        update.VariableIds = configuration.VariableIds.ToArray();
        update.StuffIds = configuration.StuffIds.ToArray();
        update.StuffIds2 = configuration.StuffIds2.ToArray();
        update.FurniSourceTypes = configuration.FurniSourceTypes.ToArray();
        update.UserSourceTypes = configuration.UserSourceTypes.ToArray();
    }

    /// <summary>Gets the registered definition and field metadata.</summary>
    public WiredFormDefinition Definition => layout.Definition;
    /// <summary>Gets the received code, including negative-variant aliases.</summary>
    public int DefinitionCode { get; }
    /// <summary>Gets the furniture ID whose configuration is being edited.</summary>
    public Id FurniId => update.FurniId;
    /// <summary>Gets the server-supplied maximum size of each furniture selection.</summary>
    public int FurnitureLimit { get; }
    /// <summary>Gets whether negative wall-item IDs may be selected.</summary>
    public bool AllowWallFurniture { get; }
    /// <summary>Gets the primary furniture selection.</summary>
    public IReadOnlyList<Id> SelectedFurniture => Array.AsReadOnly(update.StuffIds.ToArray());
    /// <summary>Gets the secondary furniture selection.</summary>
    public IReadOnlyList<Id> SecondaryFurniture => Array.AsReadOnly(update.StuffIds2.ToArray());
    /// <summary>Gets the selected furniture sources, preserving unknown numeric values.</summary>
    public IReadOnlyList<WiredFurnitureSource> FurnitureSources => Array.AsReadOnly(update.FurniSourceTypes.Select(value => (WiredFurnitureSource)value).ToArray());
    /// <summary>Gets the selected user sources, preserving unknown numeric values.</summary>
    public IReadOnlyList<WiredUserSource> UserSources => Array.AsReadOnly(update.UserSourceTypes.Select(value => (WiredUserSource)value).ToArray());
    /// <summary>Gets the server-supplied allowed furniture sources for each selection.</summary>
    public IReadOnlyList<IReadOnlyList<WiredFurnitureSource>> AllowedFurnitureSources => Array.AsReadOnly(
        sources.AllowedFurniSources.Select(values => (IReadOnlyList<WiredFurnitureSource>)Array.AsReadOnly(values.Select(value => (WiredFurnitureSource)value).ToArray())).ToArray());
    /// <summary>Gets the server-supplied allowed user sources for each selection.</summary>
    public IReadOnlyList<IReadOnlyList<WiredUserSource>> AllowedUserSources => Array.AsReadOnly(
        sources.AllowedUserSources.Select(values => (IReadOnlyList<WiredUserSource>)Array.AsReadOnly(values.Select(value => (WiredUserSource)value).ToArray())).ToArray());
    /// <summary>Gets the default furniture source for each selection as supplied by the hotel.</summary>
    public IReadOnlyList<WiredFurnitureSource> DefaultFurnitureSources => Array.AsReadOnly(sources.DefaultFurniSources.Select(value => (WiredFurnitureSource)value).ToArray());
    /// <summary>Gets the default user source for each selection as supplied by the hotel.</summary>
    public IReadOnlyList<WiredUserSource> DefaultUserSources => Array.AsReadOnly(sources.DefaultUserSources.Select(value => (WiredUserSource)value).ToArray());

    /// <summary>Reads a named integer or checkbox default supplied by the hotel.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The typed default, or null when this field has no corresponding received default.</returns>
    public WiredFormValue? GetDefaultField(string name)
    {
        WiredFieldLayout field = find_field(name);
        if (field.Storage != WiredFieldStorage.Integer || field.Index >= defaults.Length)
            return null;
        int value = defaults[field.Index];
        return field.Definition.Kind == WiredFormFieldKind.Boolean
            ? new(Boolean: field.Nonzero ? value != 0 : value == 1)
            : new(Integer: value);
    }

    /// <summary>Creates the matching typed form, or an unknown form that retains raw configuration access.</summary>
    /// <param name="configuration">The received configuration.</param>
    /// <returns>An independent editable view.</returns>
    public static WiredForm Read(WiredConfig configuration) => WiredFormRegistry.Create(configuration);

    /// <summary>Replaces a furniture selection after checking the received limit and wall-item permission.</summary>
    /// <param name="furniture">The selected item IDs.</param>
    /// <param name="secondary">Whether to replace the secondary selection.</param>
    public void SelectFurniture(IReadOnlyList<Id> furniture, bool secondary = false)
    {
        ArgumentNullException.ThrowIfNull(furniture);
        Id[] selected = furniture.Distinct().ToArray();
        if (selected.Length > FurnitureLimit)
            throw new ArgumentOutOfRangeException(nameof(furniture), "The selection exceeds the received furniture limit.");
        foreach (Id id in selected)
        {
            _ = WiredWire.FlashId(id);
            if ((long)id < 0 && !AllowWallFurniture)
                throw new ArgumentException("This definition does not allow wall furniture.", nameof(furniture));
        }
        if (secondary)
            update.StuffIds2 = selected;
        else
            update.StuffIds = selected;
    }

    /// <summary>Selects a furniture source allowed by the received definition.</summary>
    /// <param name="selection">The source selection index shown by AllowedFurnitureSources.</param>
    /// <param name="source">The source to select.</param>
    public void SelectFurnitureSource(int selection, WiredFurnitureSource source) =>
        update.FurniSourceTypes = select_source(selection, (int)source, sources.AllowedFurniSources, update.FurniSourceTypes, sources.DefaultFurniSources);

    /// <summary>Selects a user source allowed by the received definition.</summary>
    /// <param name="selection">The source selection index shown by AllowedUserSources.</param>
    /// <param name="source">The source to select.</param>
    public void SelectUserSource(int selection, WiredUserSource source) =>
        update.UserSourceTypes = select_source(selection, (int)source, sources.AllowedUserSources, update.UserSourceTypes, sources.DefaultUserSources);

    /// <summary>Gets a named field as a typed value. Reading does not normalize its stored bytes.</summary>
    /// <param name="name">The field name from Definition.Fields.</param>
    /// <returns>The typed field value.</returns>
    public WiredFormValue GetField(string name)
    {
        WiredFieldLayout field = find_field(name);
        return field.Definition.Kind switch
        {
            WiredFormFieldKind.Integer => new(Integer: integer_at(field.Index)),
            WiredFormFieldKind.Boolean => new(Boolean: field.Nonzero ? integer_at(field.Index) != 0 : integer_at(field.Index) == 1),
            WiredFormFieldKind.Text or WiredFormFieldKind.Variable => new(Text: text_at(field)),
            WiredFormFieldKind.Neighborhood => new(Tiles: WiredFormPacking.ReadNeighborhood(update.IntParams.Skip(field.Index).ToArray())),
            WiredFormFieldKind.Rewards => new(Rewards: WiredFormPacking.ReadRewards(update.StringParam)),
            _ => throw new InvalidOperationException("Unsupported field kind.")
        };
    }

    /// <summary>Edits a named field and validates its verified bounds. Unrelated and unknown fields are preserved.</summary>
    /// <param name="name">The field name from Definition.Fields.</param>
    /// <param name="value">Exactly one typed value matching the field's kind.</param>
    public void SetField(string name, WiredFormValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredFieldLayout field = find_field(name);
        if (!field.Definition.Writable)
            throw new ArgumentException("This field is derived from another control.", nameof(name));
        int count = (value.Integer.HasValue ? 1 : 0) + (value.Boolean.HasValue ? 1 : 0) +
            (value.Text is not null ? 1 : 0) + (value.Tiles is not null ? 1 : 0) + (value.Rewards is not null ? 1 : 0) + (value.Choice is not null ? 1 : 0);
        if (count != 1)
            throw new ArgumentException("Supply exactly one typed field value.", nameof(value));
        if (value.Choice is { } choice)
        {
            IReadOnlyList<WiredFormChoice> choices = WiredFormChoices.Get(Definition, name);
            string[] names = choice.Split(',', StringSplitOptions.TrimEntries);
            if (names.Length > 1 && !WiredFormChoices.IsFlags(Definition, name))
                throw new ArgumentException("Only flags accept multiple named choices.", nameof(value));
            int number = 0;
            foreach (string option in names)
            {
                WiredFormChoice selected = choices.FirstOrDefault(candidate => candidate.Name == option)
                    ?? throw new ArgumentException($"Unknown choice '{option}' for '{name}'.", nameof(value));
                number |= selected.Value;
            }
            value = new(Integer: number);
        }
        switch (field.Definition.Kind)
        {
            case WiredFormFieldKind.Integer when value.Integer is { } number:
                if (field.Minimum is { } minimum && number < minimum || field.Maximum is { } maximum && number > maximum)
                    throw new ArgumentOutOfRangeException(nameof(value), field.Definition.Values);
                put_integer(field.Index, number);
                foreach (WiredFieldLayout derived in layout.Fields.Where(candidate => candidate.DerivedFrom == field.Index))
                    put_integer(derived.Index, number < 0 ? -1 : 0);
                break;
            case WiredFormFieldKind.Boolean when value.Boolean is { } flag:
                put_integer(field.Index, flag ? 1 : 0);
                break;
            case WiredFormFieldKind.Text or WiredFormFieldKind.Variable when value.Text is { } text:
                if (field.NormalizeName)
                    text = text.Replace(' ', '_').ToLowerInvariant();
                if (field.MaximumLength is { } length && text.Length > length)
                    throw new ArgumentException(field.Definition.Values, nameof(value));
                put_text(field, text);
                break;
            case WiredFormFieldKind.Neighborhood when value.Tiles is { } tiles:
                IReadOnlyList<int> words = WiredFormPacking.WriteNeighborhood(tiles);
                for (int index = 0; index < words.Count; index++)
                    put_integer(field.Index + index, words[index]);
                break;
            case WiredFormFieldKind.Rewards when value.Rewards is { } rewards:
                update.StringParam = WiredFormPacking.WriteRewards(rewards, integer_at(1) == 1);
                break;
            default:
                throw new ArgumentException($"Field '{name}' requires {field.Definition.Kind}.", nameof(value));
        }
    }

    /// <summary>Applies named edits atomically; an invalid edit leaves all fields unchanged.</summary>
    /// <param name="edits">The edits in application order.</param>
    public void SetFields(IReadOnlyList<WiredFormEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        IReadOnlyList<int> integers = update.IntParams;
        IReadOnlyList<string> variables = update.VariableIds;
        string text = update.StringParam;
        try
        {
            foreach (WiredFormEdit edit in edits)
            {
                ArgumentNullException.ThrowIfNull(edit);
                SetField(edit.Name, edit.Value);
            }
        }
        catch
        {
            update.IntParams = integers;
            update.VariableIds = variables;
            update.StringParam = text;
            throw;
        }
    }

    /// <summary>Reads the four fields of a value-or-variable control.</summary>
    /// <param name="sourceField">The constant-or-variable discriminator field.</param>
    /// <param name="valueField">The constant value field.</param>
    /// <param name="domainField">The source domain field.</param>
    /// <param name="variableField">The variable identifier field.</param>
    /// <returns>The typed control value.</returns>
    protected WiredValueReference ReadReference(string sourceField, string valueField, string domainField, string variableField) => new(
        (WiredValueSource)GetField(sourceField).Integer.GetValueOrDefault(),
        GetField(valueField).Integer.GetValueOrDefault(),
        (WiredSourceDomain)GetField(domainField).Integer.GetValueOrDefault(),
        GetField(variableField).Text!);

    /// <summary>Writes the four fields of a value-or-variable control atomically.</summary>
    /// <param name="sourceField">The constant-or-variable discriminator field.</param>
    /// <param name="valueField">The constant value field.</param>
    /// <param name="domainField">The source domain field.</param>
    /// <param name="variableField">The variable identifier field.</param>
    /// <param name="value">The typed control value.</param>
    protected void WriteReference(string sourceField, string valueField, string domainField, string variableField, WiredValueReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetFields([
            new(sourceField, new(Integer: (int)value.Source)),
            new(valueField, new(Integer: value.Value)),
            new(domainField, new(Integer: (int)value.Domain)),
            new(variableField, new(Text: value.VariableId))
        ]);
    }

    /// <summary>Creates a detached save message containing all edited and retained values.</summary>
    /// <returns>The matching existing category-specific save model.</returns>
    public WiredConfigWrite ToUpdate() => update with
    {
        IntParams = update.IntParams.ToArray(),
        VariableIds = update.VariableIds.ToArray(),
        StuffIds = update.StuffIds.ToArray(),
        StuffIds2 = update.StuffIds2.ToArray(),
        FurniSourceTypes = update.FurniSourceTypes.ToArray(),
        UserSourceTypes = update.UserSourceTypes.ToArray()
    };

    /// <summary>Gets or sets the action delay in half-second pulses.</summary>
    public int? ActionDelay
    {
        get => update is UpdateAction action ? action.Delay : null;
        set
        {
            if (update is not UpdateAction action)
                throw new InvalidOperationException("This form is not an action.");
            if (value is not { } delay)
                throw new ArgumentNullException(nameof(value));
            ArgumentOutOfRangeException.ThrowIfNegative(delay);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(delay, 20);
            action.Delay = delay;
        }
    }

    /// <summary>Gets or sets the condition's quantifier code.</summary>
    public int? ConditionQuantifier
    {
        get => update is UpdateCondition condition ? condition.Quantifier : null;
        set
        {
            if (update is not UpdateCondition condition)
                throw new InvalidOperationException("This form is not a condition.");
            condition.Quantifier = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    /// <summary>Gets or sets whether the selector acts as a filter.</summary>
    public bool? SelectorFilter
    {
        get => update is UpdateSelector selector ? selector.IsFilter : null;
        set
        {
            if (update is not UpdateSelector selector)
                throw new InvalidOperationException("This form is not a selector.");
            selector.IsFilter = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    /// <summary>Gets or sets whether the selector is inverted.</summary>
    public bool? SelectorInvert
    {
        get => update is UpdateSelector selector ? selector.IsInvert : null;
        set
        {
            if (update is not UpdateSelector selector)
                throw new InvalidOperationException("This form is not a selector.");
            selector.IsInvert = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    private WiredFieldLayout find_field(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return layout.Fields.FirstOrDefault(field => field.Definition.Name == name)
            ?? throw new ArgumentException($"Unknown field '{name}' for {Definition.Name}.", nameof(name));
    }

    private int integer_at(int index) => index < update.IntParams.Count ? update.IntParams[index] : 0;

    private string text_at(WiredFieldLayout field) => field.Storage switch
    {
        WiredFieldStorage.Variable => field.Index < update.VariableIds.Count ? update.VariableIds[field.Index] : "n",
        WiredFieldStorage.TextPart => update.StringParam.Split('\t').ElementAtOrDefault(field.Index) ?? "",
        WiredFieldStorage.TextTail => update.StringParam.IndexOf('\t') is int split && split >= 0 ? update.StringParam[(split + 1)..] : "",
        _ => update.StringParam
    };

    private void put_integer(int index, int value)
    {
        int[] values = new int[Math.Max(update.IntParams.Count, index + 1)];
        for (int current = 0; current < update.IntParams.Count; current++)
            values[current] = update.IntParams[current];
        values[index] = value;
        update.IntParams = values;
    }

    private void put_text(WiredFieldLayout field, string value)
    {
        if (field.Storage == WiredFieldStorage.Variable)
        {
            string[] values = Enumerable.Repeat("n", Math.Max(update.VariableIds.Count, field.Index + 1)).ToArray();
            for (int index = 0; index < update.VariableIds.Count; index++)
                values[index] = update.VariableIds[index];
            values[field.Index] = value;
            update.VariableIds = values;
        }
        else if (field.Storage == WiredFieldStorage.TextPart)
        {
            if (value.Contains('\t'))
                throw new ArgumentException("A tab-separated component cannot contain a tab.", nameof(value));
            List<string> values = update.StringParam.Split('\t').ToList();
            while (values.Count <= field.Index)
                values.Add("");
            values[field.Index] = value;
            update.StringParam = string.Join('\t', values);
        }
        else if (field.Storage == WiredFieldStorage.TextTail)
            update.StringParam = update.StringParam.Split('\t')[0] + '\t' + value;
        else
            update.StringParam = value;
    }

    private static int[] select_source(int selection, int source, IReadOnlyList<IReadOnlyList<int>> allowed, IReadOnlyList<int> current, IReadOnlyList<int> defaults)
    {
        if (selection < 0 || selection >= allowed.Count || !allowed[selection].Contains(source))
            throw new ArgumentOutOfRangeException(nameof(source), "The received definition does not allow this source selection.");
        int[] result = new int[Math.Max(current.Count, selection + 1)];
        for (int index = 0; index < result.Length; index++)
            result[index] = index < current.Count ? current[index] : index < defaults.Count ? defaults[index] : 0;
        result[selection] = source;
        return result;
    }
}

/// <summary>An unrecognized form whose original values remain available through ToUpdate.</summary>
public sealed class UnknownWiredForm : WiredForm
{
    internal UnknownWiredForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}
