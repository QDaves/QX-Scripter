namespace Qx.Unity;

public sealed class UnityHeaderExtractor
{
    readonly UnityHeaderNameDatabase _names;
    readonly int _minimum_enum_members;
    readonly double _minimum_confidence;

    public UnityHeaderExtractor(
        UnityHeaderNameDatabase? names = null,
        int minimum_enum_members = 128,
        double minimum_confidence = 0.45)
    {
        if (minimum_enum_members < 2)
            throw new ArgumentOutOfRangeException(nameof(minimum_enum_members));
        if (minimum_confidence is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimum_confidence));
        _names = names ?? UnityHeaderNameDatabase.LoadDefault();
        _minimum_enum_members = minimum_enum_members;
        _minimum_confidence = minimum_confidence;
    }

    public UnityMessageMap Extract(string path)
    {
        UnityClientLayout layout = UnityClientLayout.Locate(path);
        return ExtractMetadata(layout.MetadataPath);
    }

    public UnityMessageMap ExtractMetadata(string metadata_path)
    {
        Il2CppMetadataReader metadata = Il2CppMetadataReader.Load(metadata_path);
        IReadOnlyList<Il2CppEnumDefinition> candidates = metadata.ReadEnums(_minimum_enum_members);
        CandidatePair pair = SelectCandidates(metadata, candidates);

        return new UnityMessageMap
        {
            MetadataVersion = metadata.Version,
            MetadataSha256 = metadata.Sha256,
            MetadataLength = metadata.Length,
            IncomingEnum = pair.Incoming.QualifiedName,
            OutgoingEnum = pair.Outgoing.QualifiedName,
            DirectionEvidence = pair.DirectionEvidence,
            DirectionsVerified = pair.DirectionsVerified,
            IncomingInventorySimilarity = pair.IncomingSimilarity,
            OutgoingInventorySimilarity = pair.OutgoingSimilarity,
            IncomingReferenceCount = _names.Incoming.Count,
            OutgoingReferenceCount = _names.Outgoing.Count,
            IncomingReferenceMatchCount = ReferenceMatches(pair.Incoming, _names.Incoming.Keys),
            OutgoingReferenceMatchCount = ReferenceMatches(pair.Outgoing, _names.Outgoing.Keys),
            Incoming = CreateHeaders(pair.Incoming, UnityHeaderDirection.Incoming),
            Outgoing = CreateHeaders(pair.Outgoing, UnityHeaderDirection.Outgoing)
        };
    }

    CandidatePair SelectCandidates(
        Il2CppMetadataReader metadata,
        IReadOnlyList<Il2CppEnumDefinition> candidates)
    {
        if (candidates.Count < 2)
            throw new InvalidDataException("Could not locate both Unity protocol header enums.");

        Il2CppProtocolEnumPair? structural = metadata.FindProtocolEnumPair(candidates);
        if (structural is not null)
        {
            Il2CppEnumDefinition incoming = candidates.Single(candidate =>
                candidate.TypeIndex == structural.IncomingTypeIndex);
            Il2CppEnumDefinition outgoing = candidates.Single(candidate =>
                candidate.TypeIndex == structural.OutgoingTypeIndex);
            return new CandidatePair(
                incoming,
                outgoing,
                Similarity(incoming, _names.Incoming.Keys),
                Similarity(outgoing, _names.Outgoing.Keys),
                $"MetadataConstructorSignature:{structural.AttributeType}",
                true);
        }

        CandidatePair? best = null;
        foreach (Il2CppEnumDefinition incoming in candidates)
        {
            double incoming_similarity = Similarity(incoming, _names.Incoming.Keys);
            foreach (Il2CppEnumDefinition outgoing in candidates)
            {
                if (ReferenceEquals(incoming, outgoing))
                    continue;
                double outgoing_similarity = Similarity(outgoing, _names.Outgoing.Keys);
                double cross_score = Similarity(incoming, _names.Outgoing.Keys) + Similarity(outgoing, _names.Incoming.Keys);
                double score = incoming_similarity + outgoing_similarity - cross_score * 0.2;
                if (best is null || score > best.SelectionScore)
                {
                    best = new CandidatePair(
                        incoming,
                        outgoing,
                        incoming_similarity,
                        outgoing_similarity,
                        "ReferenceInventoryFallback",
                        false,
                        score);
                }
            }
        }

        if (best is null ||
            best.IncomingSimilarity < _minimum_confidence ||
            best.OutgoingSimilarity < _minimum_confidence)
        {
            throw new InvalidDataException(
                $"Unity protocol header enums could not be identified structurally or by reference inventory " +
                $"(incoming similarity {best?.IncomingSimilarity:F3}, " +
                $"outgoing similarity {best?.OutgoingSimilarity:F3}).");
        }
        return best;
    }

    IReadOnlyList<UnityHeaderDefinition> CreateHeaders(
        Il2CppEnumDefinition definition,
        UnityHeaderDirection direction)
    {
        var by_id = new Dictionary<short, UnityHeaderDefinition>();
        foreach (Il2CppEnumMember member in definition.Members)
        {
            if (member.Value < 0)
                continue;
            UnityHeaderNames? names = _names.Find(direction, member.Value);
            var header = new UnityHeaderDefinition(
                member.Value,
                names?.Name,
                names?.FlashName,
                member.Name,
                member.Ordinal);
            if (!by_id.TryAdd(member.Value, header))
                throw new InvalidDataException(
                    $"Unity {direction} enum '{definition.QualifiedName}' contains duplicate header {member.Value}.");
        }
        return by_id.Values.OrderBy(header => header.Id).ToArray();
    }

    static double Similarity(Il2CppEnumDefinition definition, IEnumerable<short> known_ids)
    {
        var candidate = definition.Members.Where(member => member.Value >= 0).Select(member => member.Value).ToHashSet();
        var known = known_ids.ToHashSet();
        int intersection = candidate.Count(known.Contains);
        int union = candidate.Count + known.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    static int ReferenceMatches(Il2CppEnumDefinition definition, IEnumerable<short> known_ids)
    {
        var candidate = definition.Members
            .Where(member => member.Value >= 0)
            .Select(member => member.Value)
            .ToHashSet();
        return known_ids.Count(candidate.Contains);
    }

    sealed record CandidatePair(
        Il2CppEnumDefinition Incoming,
        Il2CppEnumDefinition Outgoing,
        double IncomingSimilarity,
        double OutgoingSimilarity,
        string DirectionEvidence,
        bool DirectionsVerified,
        double SelectionScore = 0);
}
