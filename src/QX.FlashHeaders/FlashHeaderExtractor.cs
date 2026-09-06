using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public static class FlashHeaderExtractor
{
    const int MinimumDirectionRegistrations = 5;

    public static FlashHeaderMap Extract(
        string path,
        SignatureDatabase? names = null)
    {
        SwfInfo swf = SwfLoader.Load(path);
        try
        {
            FlashHeaderMap map = Extract(swf);
            if (names is not null)
                new FlashHeaderNameResolver(swf, names).Apply(map);
            map.Own(swf);
            return map;
        }
        catch
        {
            try
            {
                swf.Dispose();
            }
            catch
            {
            }
            throw;
        }
    }

    public static FlashHeaderMap Extract(SwfInfo swf)
    {
        using IDisposable identities =
            Avm2MethodAnalyzer.CacheRuntimeIdentities();
        var types = new Avm2CallTargetResolver(
            swf.DeclaringScopes,
            swf.AuthenticatedHarmanTransform);
        var candidates = new List<ConfigurationCandidate>();
        foreach (ABCFile abc in swf.AbcFiles)
        {
            foreach (ASClass config in abc.Classes)
            {
                List<Registration> registrations =
                    ReadRegistrations(config, types);
                if (registrations.Count < MinimumDirectionRegistrations * 2)
                    continue;

                ConfigurationCandidate? candidate = BuildCandidate(config, registrations, types);
                if (candidate is not null)
                    candidates.Add(candidate);
            }
        }

        ConfigurationCandidate selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Registrations.Count)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No structurally valid message configuration was found in any DoABC tag.");

        FlashHeaderMap map = BuildMap(
            selected,
            candidates.Count,
            types,
            swf.AbcFiles);
        map.Types = types;
        map.SourceSha256 = swf.SourceContainerSha256;
        map.BuildIds = FlashClientBuildIdentity.FromAbcConstants(swf);
        return map;
    }

    static ConfigurationCandidate? BuildCandidate(
        ASClass config,
        List<Registration> registrations,
        Avm2CallTargetResolver types)
    {
        List<FieldScore> fields = registrations
            .GroupBy(registration => registration.FieldTrait)
            .Select(group => ScoreField(
                group.Key,
                group.ToList(),
                types))
            .OfType<FieldScore>()
            .Where(field => field.Registrations.Count >= MinimumDirectionRegistrations)
            .ToList();
        if (fields.Count < 2 ||
            fields.Select(field => field.Name)
                .Distinct(StringComparer.Ordinal)
                .Count() != fields.Count)
            return null;

        string? outgoing_field = fields
            .OrderByDescending(field => field.OutgoingScore - field.IncomingScore)
            .ThenByDescending(field => field.OutgoingScore)
            .ThenByDescending(field => field.Registrations.Count)
            .FirstOrDefault(field => field.OutgoingScore > field.IncomingScore)?.Name;
        string? incoming_field = fields
            .Where(field => field.Name != outgoing_field)
            .OrderByDescending(field => field.IncomingScore - field.OutgoingScore)
            .ThenByDescending(field => field.IncomingScore)
            .ThenByDescending(field => field.Registrations.Count)
            .FirstOrDefault(field => field.IncomingScore > field.OutgoingScore)?.Name;

        ResolveFieldsFromGetters(
            config.Instance,
            fields,
            types,
            ref incoming_field,
            ref outgoing_field);
        ResolveFieldsByConvention(fields, ref incoming_field, ref outgoing_field);

        if (incoming_field is null || outgoing_field is null || incoming_field == outgoing_field)
            return null;

        FieldScore incoming = fields.First(field => field.Name == incoming_field);
        FieldScore outgoing = fields.First(field => field.Name == outgoing_field);
        int classified = incoming.IncomingScore + outgoing.OutgoingScore;
        int contradicted = incoming.OutgoingScore + outgoing.IncomingScore;
        int score = registrations.Count + classified * 4 - contradicted * 8;
        if (ImplementsInterface(config.Instance, "IMessageConfiguration", types))
            score += 10000;

        return new ConfigurationCandidate(config, registrations, incoming_field, outgoing_field, score);
    }

    static FlashHeaderMap BuildMap(
        ConfigurationCandidate candidate,
        int candidate_count,
        Avm2CallTargetResolver types,
        IReadOnlyList<ABCFile> abc_files)
    {
        ASInstance instance = candidate.Config.Instance;
        ASNamespace config_namespace = instance.QName.Namespace ??
            throw new InvalidDataException(
                "The selected message configuration QName has no namespace.");
        var map = new FlashHeaderMap
        {
            ConfigClass = $"{config_namespace.RuntimeName}::{instance.QName.RuntimeName}",
            IncomingField = candidate.IncomingField,
            OutgoingField = candidate.OutgoingField,
            CandidateClassCount = candidate_count
        };

        var incoming = new Dictionary<int, FlashHeaderDefinition>();
        var outgoing = new Dictionary<int, FlashHeaderDefinition>();
        foreach (Registration registration in candidate.Registrations)
        {
            MessageDirection? direction = registration.Field switch
            {
                var field when field == candidate.IncomingField => MessageDirection.Incoming,
                var field when field == candidate.OutgoingField => MessageDirection.Outgoing,
                _ => null
            };
            if (direction is null)
            {
                map.UnclassifiedRegistrationCount++;
                continue;
            }

            IReadOnlyList<Avm2TypeDefinition> definitions = types.ResolveTypes(
                registration.MessageClass,
                registration.MessageAbc);
            IReadOnlyList<string> constructor_parameter_types = ConstructorParameterTypes(
                definitions,
                out bool constructor_signature_resolved);
            var definition = new FlashHeaderDefinition
            {
                Id = registration.Id,
                Direction = direction.Value,
                Class = registration.MessageClass.RuntimeName,
                Namespace = registration.MessageClass.Namespace?.RuntimeName ?? "",
                RegistrationType = registration.MessageClass,
                RegistrationAbc = registration.MessageAbc,
                RegistrationAbcIndex = IndexOf(
                    abc_files,
                    registration.MessageAbc),
                RegistrationConfiguration = candidate.Config,
                RegistrationField = registration.FieldTrait.QName,
                TypeDefinitions = definitions,
                ConstructorSignatureResolved = constructor_signature_resolved,
                ConstructorParameterTypes = constructor_parameter_types
            };

            Dictionary<int, FlashHeaderDefinition> target = direction == MessageDirection.Outgoing ? outgoing : incoming;
            if (target.TryGetValue(definition.Id, out FlashHeaderDefinition? existing))
            {
                if (!SameRegistration(existing, definition))
                    throw new InvalidDataException(
                        $"Conflicting {direction} registrations for header {definition.Id}: " +
                        $"'{existing.Qualified}' and '{definition.Qualified}'.");
                map.DuplicateRegistrationCount++;
                continue;
            }
            target.Add(definition.Id, definition);
        }

        map.Incoming.AddRange(incoming.Values.OrderBy(message => message.Id));
        map.Outgoing.AddRange(outgoing.Values.OrderBy(message => message.Id));
        if (map.Incoming.Count == 0 || map.Outgoing.Count == 0)
            throw new InvalidDataException("Message configuration did not yield both incoming and outgoing headers.");
        return map;
    }

    static IReadOnlyList<string> ConstructorParameterTypes(
        IReadOnlyList<Avm2TypeDefinition> definitions,
        out bool resolved)
    {
        if (definitions.Count != 1)
        {
            resolved = false;
            return [];
        }
        ASMethod? constructor = definitions[0].Instance.Constructor;
        if (constructor is null)
        {
            resolved = false;
            return [];
        }
        resolved = true;
        return constructor.Parameters
            .Select(parameter => Avm2MethodAnalyzer.Qualified(parameter.Type))
            .ToArray();
    }

    static int IndexOf(
        IReadOnlyList<ABCFile> values,
        ABCFile value)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (ReferenceEquals(values[index], value))
                return index;
        }
        return -1;
    }

    static bool SameRegistration(
        FlashHeaderDefinition left,
        FlashHeaderDefinition right)
    {
        if (left.RegistrationType is null ||
            right.RegistrationType is null ||
            Avm2MethodAnalyzer.RuntimeSymbolIdentity(left.RegistrationType) !=
                Avm2MethodAnalyzer.RuntimeSymbolIdentity(right.RegistrationType) ||
            left.TypeDefinitions.Count != right.TypeDefinitions.Count)
        {
            return false;
        }
        return left.TypeDefinitions.All(left_definition =>
            right.TypeDefinitions.Any(right_definition =>
                ReferenceEquals(left_definition.Abc, right_definition.Abc) &&
                ReferenceEquals(left_definition.Instance, right_definition.Instance)));
    }

    internal static List<Registration> ReadRegistrations(
        ASClass config,
        Avm2CallTargetResolver types)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(types);
        FlowContext? context = Analyze(
            config.Constructor,
            config,
            types);
        if (context is null ||
            !context.Flow.Complete ||
            !context.Analysis.ControlFlow.Complete)
            return [];

        var registrations = new List<Registration>();
        for (int setter_index = 0;
            setter_index < context.Code.Count;
            setter_index++)
        {
            if (context.Code[setter_index] is not SetPropertyIns setter ||
                setter.PropertyName?.Kind != MultinameKind.MultinameL)
                continue;

            int value_index = PreviousRelevant(
                context.Code,
                setter_index - 1);
            int id_index = PreviousRelevant(
                context.Code,
                value_index - 1);
            int field_index = PreviousRelevant(
                context.Code,
                id_index - 1);
            if (value_index < 0 || id_index < 0 || field_index < 0)
                continue;
            if (context.Code[value_index] is not GetLexIns value_class ||
                value_class.TypeName is null)
                continue;
            if (context.Code[field_index] is not GetLexIns field ||
                field.TypeName is null)
                continue;
            if (!TryReadId(context.Code[id_index], out int id))
                continue;
            if (!context.Operations.TryGetValue(
                    setter_index,
                    out Avm2DataFlowOperation? setter_operation) ||
                !context.Operations.TryGetValue(
                    field_index,
                    out Avm2DataFlowOperation? field_operation) ||
                !context.Operations.TryGetValue(
                    id_index,
                    out Avm2DataFlowOperation? id_operation) ||
                !context.Operations.TryGetValue(
                    value_index,
                    out Avm2DataFlowOperation? value_operation) ||
                setter_operation.Inputs.Count != 3 ||
                field_operation.Outputs.Count != 1 ||
                id_operation.Outputs.Count != 1 ||
                value_operation.Outputs.Count != 1 ||
                setter_operation.Inputs[0] != field_operation.Outputs[0] ||
                setter_operation.Inputs[1] != id_operation.Outputs[0] ||
                setter_operation.Inputs[2] != value_operation.Outputs[0])
            {
                continue;
            }
            ASTrait? field_trait = ResolveRegistrationField(
                config,
                field.TypeName,
                field_operation,
                context,
                types);
            Avm2TypeDefinition? message = ResolveRegistrationClass(
                value_class.TypeName,
                value_operation,
                context,
                types);
            if (field_trait is null || !message.HasValue)
                continue;
            Avm2TypeDefinition message_type = message.Value;

            registrations.Add(new Registration(
                config,
                field_trait,
                id,
                message_type.Instance.QName,
                message_type.Abc));
        }
        return registrations;
    }

    static ASTrait? ResolveRegistrationField(
        ASClass config,
        ASMultiname property,
        Avm2DataFlowOperation operation,
        FlowContext context,
        Avm2CallTargetResolver types)
    {
        ASTrait[] fields = config.Traits
            .Where(trait =>
                trait.Kind is TraitKind.Slot or TraitKind.Constant &&
                Avm2CallTargetResolver.PropertiesMatch(
                    property,
                    context.Method.ABC,
                    trait.QName,
                    trait.ABC))
            .Take(2)
            .ToArray();
        if (fields.Length != 1)
            return null;
        return ProvesLexicalTrait(
                context,
                operation,
                property,
                config,
                fields[0],
                types)
            ? fields[0]
            : null;
    }

    static Avm2TypeDefinition? ResolveRegistrationClass(
        ASMultiname property,
        Avm2DataFlowOperation operation,
        FlowContext context,
        Avm2CallTargetResolver types)
    {
        Avm2TypeDefinition? declared = types.ResolveUniqueType(
            property,
            context.Method.ABC);
        if (!declared.HasValue ||
            operation.Outputs.Count != 1 ||
            !ProvesLexicalClass(
                context,
                operation,
                property,
                declared.Value,
                types))
        {
            return null;
        }
        Avm2ResolvedValueSet resolved;
        try
        {
            resolved = types.ResolveValueTypes(
                context.Binding,
                context.ExactReceiver,
                operation.Outputs[0]);
        }
        catch
        {
            return null;
        }
        if (!resolved.Exhaustive ||
            resolved.Types.Count != 1 ||
            !resolved.Types[0].Static ||
            !ReferenceEquals(
                resolved.Types[0].RuntimeType,
                declared.Value.Instance))
        {
            return null;
        }
        return declared;
    }

    static bool ProvesLexicalClass(
        FlowContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        Avm2TypeDefinition expected,
        Avm2CallTargetResolver types)
    {
        if (!LexicalScopeIsComplete(
                context,
                operation,
                property,
                out IReadOnlyList<bool?> scope_with))
        {
            return false;
        }
        for (int index = operation.ScopeBefore.Count - 1;
            index >= 0;
            index--)
        {
            if (scope_with[index] != false)
                return false;
            ASContainer? owner = ResolveScopeOwner(
                context,
                operation.ScopeBefore[index],
                types);
            if (owner is null)
                continue;
            ASTrait[] matches = MatchingTraits(
                owner,
                property,
                context.Method.ABC);
            if (matches.Length == 0)
                continue;
            return matches.Length == 1 &&
                matches[0].Kind == TraitKind.Class &&
                matches[0].ClassIndex >= 0 &&
                matches[0].ClassIndex < matches[0].ABC.Classes.Count &&
                ReferenceEquals(
                    matches[0].ABC.Classes[matches[0].ClassIndex],
                    expected.Class);
        }
        return true;
    }

    static bool ProvesLexicalTrait(
        FlowContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        ASContainer expected_owner,
        ASTrait expected_trait,
        Avm2CallTargetResolver types)
    {
        if (!LexicalScopeIsComplete(
                context,
                operation,
                property,
                out IReadOnlyList<bool?> scope_with) ||
            !expected_owner.Traits.Contains(expected_trait) ||
            !Avm2CallTargetResolver.PropertiesMatch(
                property,
                context.Method.ABC,
                expected_trait.QName,
                expected_trait.ABC))
        {
            return false;
        }

        for (int index = operation.ScopeBefore.Count - 1;
            index >= 0;
            index--)
        {
            if (scope_with[index] != false)
                return false;
            ASContainer? owner = ResolveScopeOwner(
                context,
                operation.ScopeBefore[index],
                types);
            if (owner is null)
                return false;
            ASTrait[] matches = MatchingTraits(
                owner,
                property,
                context.Method.ABC);
            if (matches.Length == 0)
                continue;
            return matches.Length == 1 &&
                ReferenceEquals(owner, expected_owner) &&
                ReferenceEquals(matches[0], expected_trait);
        }
        return false;
    }

    static bool LexicalScopeIsComplete(
        FlowContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        out IReadOnlyList<bool?> scope_with)
    {
        scope_with = [];
        if (!context.Flow.Complete ||
            !context.Flow.DeclaringScopeKnown ||
            !context.Analysis.ControlFlow.Complete ||
            operation.Unreachable ||
            property.IsRuntime ||
            property.Kind is not (
                MultinameKind.QName or
                MultinameKind.QNameA) ||
            operation.ScopeBefore.Count == 0 ||
            operation.Inputs.Count != operation.ScopeBefore.Count ||
            !operation.Inputs.SequenceEqual(
                operation.ScopeBefore,
                StringComparer.Ordinal) ||
            !context.Flow.ScopeWithBefore.TryGetValue(
                operation.Instruction,
                out IReadOnlyList<bool?>? values) ||
            values.Count != operation.ScopeBefore.Count)
        {
            return false;
        }
        scope_with = values;
        return true;
    }

    static ASTrait[] MatchingTraits(
        ASContainer owner,
        ASMultiname property,
        ABCFile requester) =>
        owner.Traits
            .Where(trait =>
                Avm2CallTargetResolver.PropertiesMatch(
                    property,
                    requester,
                    trait.QName,
                    trait.ABC))
            .Take(2)
            .ToArray();

    static ASContainer? ResolveScopeOwner(
        FlowContext context,
        string value,
        Avm2CallTargetResolver types)
    {
        if (context.ScopeOwners.TryGetValue(
                value,
                out ASContainer? cached))
        {
            return cached;
        }
        ASContainer? owner = null;
        Avm2ResolvedValueSet resolved;
        try
        {
            resolved = types.ResolveValueTypes(
                context.Binding,
                context.ExactReceiver,
                value);
        }
        catch
        {
            context.ScopeOwners.Add(value, null);
            return null;
        }
        if (!resolved.Exhaustive ||
            resolved.Types.Count != 1)
        {
            context.ScopeOwners.Add(value, null);
            return null;
        }
        Avm2ResolvedValueType scope = resolved.Types[0];
        Avm2TypeDefinition? definition =
            types.ResolveType(scope.RuntimeType);
        if (definition.HasValue)
        {
            owner = scope.Static
                ? definition.Value.Class
                : definition.Value.Instance;
        }
        context.ScopeOwners.Add(value, owner);
        return owner;
    }

    static int PreviousRelevant(
        IReadOnlyList<ASInstruction> code,
        int index)
    {
        while (index >= 0 && code[index].OP is OPCode.Nop or OPCode.Debug or OPCode.DebugFile or OPCode.DebugLine)
            index--;
        return index;
    }

    static FieldScore? ScoreField(
        ASTrait trait,
        List<Registration> registrations,
        Avm2CallTargetResolver types)
    {
        if (!Avm2MethodAnalyzer.TryGetStaticName(
                trait.QName,
                out string name))
        {
            return null;
        }
        int incoming = 0;
        int outgoing = 0;
        foreach (Registration registration in registrations)
        {
            Avm2TypeDefinition? definition = types.ResolveUniqueType(
                registration.MessageClass,
                registration.MessageAbc);
            if (!definition.HasValue)
                continue;
            ASInstance instance = definition.Value.Instance;
            if (ImplementsInterface(instance, "IMessageComposer", types) || HasMethod(instance, "getMessageArray"))
                outgoing++;
            if (ImplementsInterface(instance, "IMessageEvent", types) || Inherits(instance, "MessageEvent", types))
                incoming++;
        }
        return new FieldScore(
            name,
            registrations[0].FieldOwner,
            trait,
            registrations,
            incoming,
            outgoing);
    }

    static void ResolveFieldsFromGetters(
        ASInstance instance,
        List<FieldScore> fields,
        Avm2CallTargetResolver types,
        ref string? incoming,
        ref string? outgoing)
    {
        foreach (ASTrait getter in instance.GetTraits(TraitKind.Getter))
        {
            FieldScore[] matches = fields
                .Where(field => GetterReturnsField(
                    instance,
                    getter,
                    field.Owner,
                    field.Trait,
                    types))
                .Take(2)
                .ToArray();
            if (matches.Length != 1)
                continue;
            string field = matches[0].Name;

            if (NameEquals(getter.QName, "events", StringComparison.OrdinalIgnoreCase))
                incoming ??= field;
            if (NameEquals(getter.QName, "composers", StringComparison.OrdinalIgnoreCase))
                outgoing ??= field;
        }
    }

    internal static bool GetterReturnsField(
        ASInstance instance,
        ASTrait getter,
        ASContainer field_owner,
        ASTrait field,
        Avm2CallTargetResolver types)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(field_owner);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(types);
        if (getter.Kind != TraitKind.Getter ||
            getter.Method?.Body is null ||
            !field_owner.Traits.Contains(field))
        {
            return false;
        }
        FlowContext? context = Analyze(
            getter.Method,
            instance,
            types);
        if (context is null ||
            !context.Flow.Complete ||
            !context.Analysis.ControlFlow.Complete)
        {
            return false;
        }
        Avm2DataFlowOperation[] returns = context.Flow.Operations
            .Where(operation =>
                !operation.Unreachable &&
                operation.Instruction >= 0 &&
                operation.Instruction < context.Code.Count &&
                context.Code[operation.Instruction].OP ==
                    OPCode.ReturnValue)
            .ToArray();
        return returns.Length > 0 &&
            returns.All(operation =>
                operation.Inputs.Count == 1 &&
                ReturnedValueMatchesField(
                    context,
                    operation.Inputs[0],
                    field_owner,
                    field,
                    types,
                    []));
    }

    static bool ReturnedValueMatchesField(
        FlowContext context,
        string value,
        ASContainer field_owner,
        ASTrait field,
        Avm2CallTargetResolver types,
        HashSet<string> visited)
    {
        if (!visited.Add(value))
            return false;
        if (context.Phis.TryGetValue(
                value,
                out Avm2DataFlowPhi? phi))
        {
            return phi.Inputs.Count > 0 &&
                phi.Inputs.All(input =>
                    ReturnedValueMatchesField(
                        context,
                        input.Value,
                        field_owner,
                        field,
                        types,
                        new HashSet<string>(
                            visited,
                            StringComparer.Ordinal)));
        }
        if (!context.Producers.TryGetValue(
                value,
                out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 ||
            producer.Instruction >= context.Code.Count)
        {
            return false;
        }
        ASInstruction instruction =
            context.Code[producer.Instruction];
        if (instruction is GetLexIns lexical)
        {
            return ProvesLexicalTrait(
                context,
                producer,
                lexical.TypeName,
                field_owner,
                field,
                types);
        }
        if (instruction is GetPropertyIns property)
        {
            return ProvesPropertyTrait(
                context,
                producer,
                property.PropertyName,
                field_owner,
                field,
                types);
        }
        if (producer.Inputs.Count != 1 ||
            instruction.OP is not (
                OPCode.Coerce or
                OPCode.Coerce_a or
                OPCode.AsType or
                OPCode.AsTypeLate or
                OPCode.CheckFilter or
                OPCode.Convert_b or
                OPCode.Convert_d or
                OPCode.Convert_i or
                OPCode.Convert_o or
                OPCode.Convert_s or
                OPCode.Convert_u))
        {
            return false;
        }
        return ReturnedValueMatchesField(
            context,
            producer.Inputs[0],
            field_owner,
            field,
            types,
            visited);
    }

    static bool ProvesPropertyTrait(
        FlowContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        ASContainer expected_owner,
        ASTrait expected_trait,
        Avm2CallTargetResolver types)
    {
        if (operation.Inputs.Count == 0 ||
            property.IsRuntime ||
            property.Kind is not (
                MultinameKind.QName or
                MultinameKind.QNameA) ||
            !expected_owner.Traits.Contains(expected_trait) ||
            !Avm2CallTargetResolver.PropertiesMatch(
                property,
                context.Method.ABC,
                expected_trait.QName,
                expected_trait.ABC))
        {
            return false;
        }
        ASContainer? owner = ResolveScopeOwner(
            context,
            operation.Inputs[0],
            types);
        if (!ReferenceEquals(owner, expected_owner))
            return false;
        ASTrait[] matches = owner.Traits
            .Where(trait =>
                Avm2CallTargetResolver.PropertiesMatch(
                    property,
                    context.Method.ABC,
                    trait.QName,
                    trait.ABC))
            .Take(2)
            .ToArray();
        return matches.Length == 1 &&
            ReferenceEquals(matches[0], expected_trait);
    }

    static FlowContext? Analyze(
        ASMethod method,
        ASContainer owner,
        Avm2CallTargetResolver types)
    {
        if (method.Body is null)
            return null;
        Avm2MethodBinding[] bindings = types.ResolveMethodBindings(method)
            .Where(binding =>
                binding.Resolved &&
                ReferenceEquals(binding.Owner, owner))
            .Take(2)
            .ToArray();
        if (bindings.Length != 1)
            return null;
        Avm2ExactReceiver? exact_receiver = owner switch
        {
            ASClass @class =>
                new Avm2ExactReceiver(
                    @class.Instance,
                    true),
            ASInstance value =>
                new Avm2ExactReceiver(
                    value,
                    false),
            _ => null
        };
        if (exact_receiver is null)
            return null;
        try
        {
            Avm2MethodAnalysis analysis =
                Avm2MethodAnalyzer.Analyze(method.Body);
            Avm2DataFlowAnalysis flow =
                types.DeclaringScopes.Analyze(
                    method.Body,
                    analysis,
                    bindings[0],
                    exact_receiver);
            if (!analysis.ControlFlow.Complete ||
                !flow.Complete ||
                !Avm2VerifierValidator.Validate(
                    method.Body,
                    analysis).VerifierValid)
            {
                return null;
            }
            var producers =
                new Dictionary<string, Avm2DataFlowOperation>(
                    StringComparer.Ordinal);
            foreach (Avm2DataFlowOperation operation in flow.Operations)
            {
                foreach (string definition in operation.Definitions)
                    producers.TryAdd(definition, operation);
            }
            return new FlowContext(
                method,
                bindings[0],
                exact_receiver,
                analysis,
                flow,
                analysis.DecodedCode.ToList(),
                flow.Operations.ToDictionary(
                    operation => operation.Instruction),
                producers,
                flow.Phis.ToDictionary(
                    phi => phi.Value,
                    StringComparer.Ordinal));
        }
        catch
        {
            return null;
        }
    }

    static void ResolveFieldsByConvention(
        List<FieldScore> fields,
        ref string? incoming,
        ref string? outgoing)
    {
        outgoing ??= fields.FirstOrDefault(field =>
            field.Name.Contains("composer", StringComparison.OrdinalIgnoreCase))?.Name;
        string? outgoing_value = outgoing;
        incoming ??= fields
            .Where(field => field.Name != outgoing_value)
            .OrderByDescending(field => field.Registrations.Count)
            .FirstOrDefault()?.Name;
        string? incoming_value = incoming;
        outgoing ??= fields
            .Where(field => field.Name != incoming_value)
            .OrderByDescending(field => field.Registrations.Count)
            .FirstOrDefault()?.Name;
    }

    static bool ImplementsInterface(
        ASInstance instance,
        string interface_name,
        Avm2CallTargetResolver types,
        HashSet<ASInstance>? visited = null)
    {
        visited ??= new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(instance))
            return false;

        try
        {
            foreach (ASMultiname candidate in instance.GetInterfaces())
            {
                Avm2TypeDefinition? contract = types.ResolveUniqueType(
                    candidate,
                    instance.ABC);
                if (NameEquals(contract?.Instance.QName, interface_name))
                    return true;
            }
        }
        catch
        {
        }

        if (!Avm2MethodAnalyzer.TryGetStaticName(
                instance.Super,
                out string super_name) ||
            super_name == "Object")
        {
            return false;
        }
        Avm2TypeDefinition? parent = types.ResolveUniqueType(
            instance.Super,
            instance.ABC);
        return parent.HasValue &&
            ImplementsInterface(parent.Value.Instance, interface_name, types, visited);
    }

    static bool Inherits(
        ASInstance instance,
        string class_name,
        Avm2CallTargetResolver types)
    {
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        ASMultiname? parent = instance.Super;
        ABCFile requester = instance.ABC;
        while (parent is not null)
        {
            if (!Avm2MethodAnalyzer.TryGetStaticName(
                    parent,
                    out string parent_name) ||
                parent_name == "Object")
            {
                return false;
            }
            Avm2TypeDefinition? parent_definition = types.ResolveUniqueType(
                parent,
                requester);
            if (!parent_definition.HasValue ||
                !visited.Add(parent_definition.Value.Instance))
            {
                return false;
            }
            ASInstance parent_instance = parent_definition.Value.Instance;
            if (NameEquals(parent_instance.QName, class_name))
                return true;
            parent = parent_instance.Super;
            requester = parent_instance.ABC;
        }
        return false;
    }

    static bool HasMethod(ASInstance instance, string method_name)
    {
        try
        {
            return instance.Traits.Any(trait =>
                trait.Kind is TraitKind.Method or TraitKind.Getter or TraitKind.Setter &&
                NameEquals(trait.QName, method_name));
        }
        catch
        {
            return false;
        }
    }

    static string Qualified(ASMultiname name)
    {
        string? ns = name.Namespace?.RuntimeName;
        return string.IsNullOrEmpty(ns)
            ? name.RuntimeName
            : $"{ns}.{name.RuntimeName}";
    }

    static bool NameEquals(
        ASMultiname? name,
        string expected,
        StringComparison comparison = StringComparison.Ordinal) =>
        Avm2MethodAnalyzer.TryGetStaticName(name, out string value) &&
        string.Equals(value, expected, comparison);

    static bool TryReadId(ASInstruction ins, out int id)
    {
        switch (ins)
        {
            case PushByteIns b: id = b.Value; return true;
            case PushShortIns s: id = s.Value; return true;
            case PushIntIns p: id = p.Value; return true;
            case PushUIntIns p when p.Value <= int.MaxValue: id = (int)p.Value; return true;
            case PushDoubleIns p when p.Value >= 0 && p.Value <= int.MaxValue && p.Value == Math.Truncate(p.Value):
                id = (int)p.Value;
                return true;
            default: id = 0; return false;
        }
    }

    internal sealed record Registration(
        ASContainer FieldOwner,
        ASTrait FieldTrait,
        int Id,
        ASMultiname MessageClass,
        ABCFile MessageAbc)
    {
        public string? Field =>
            Avm2MethodAnalyzer.TryGetStaticName(
                FieldTrait.QName,
                out string name)
                ? name
                : null;
    }

    sealed record FieldScore(
        string Name,
        ASContainer Owner,
        ASTrait Trait,
        List<Registration> Registrations,
        int IncomingScore,
        int OutgoingScore);

    sealed record ConfigurationCandidate(
        ASClass Config,
        List<Registration> Registrations,
        string IncomingField,
        string OutgoingField,
        int Score);

    sealed record FlowContext(
        ASMethod Method,
        Avm2MethodBinding Binding,
        Avm2ExactReceiver ExactReceiver,
        Avm2MethodAnalysis Analysis,
        Avm2DataFlowAnalysis Flow,
        IReadOnlyList<ASInstruction> Code,
        IReadOnlyDictionary<int, Avm2DataFlowOperation> Operations,
        IReadOnlyDictionary<string, Avm2DataFlowOperation> Producers,
        IReadOnlyDictionary<string, Avm2DataFlowPhi> Phis)
    {
        public Dictionary<string, ASContainer?> ScopeOwners { get; } =
            new(StringComparer.Ordinal);
    }
}
