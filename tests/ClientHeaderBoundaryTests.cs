using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Qx;
using Qx.ClientCatalog;
using Qx.ClientCatalog.InstalledClients;
using Qx.Diagnostics;
using Qx.Game.Protocol;
using Qx.Headers.Flash;
using Qx.Interception.GEarth;
using Qx.Messages;
using Qx.Protocol;
using Xunit;

namespace QX.Tests;

public sealed class ClientHeaderBoundaryTests
{
    static readonly string Repository = FindRepository();
    static readonly string ClientHeaders = Path.Combine(
        Repository,
        "src",
        "QX.ClientHeaders",
        "QX.ClientHeaders.csproj");
    static readonly string ClientCatalog = Path.Combine(
        Repository,
        "src",
        "QX.ClientCatalog",
        "QX.ClientCatalog.csproj");
    static readonly (string Key, Direction Direction, string FlashName)[] FlashSemanticGaps =
    [
        ("room.lifecycle.quit", Direction.Out, "Quit"),
        ("inventory.furni.request", Direction.Out, "RequestFurniInventory"),
        ("inventory.furni.invalidated", Direction.In, "FurniListInvalidate"),
        ("inventory.pets.request", Direction.Out, "GetPetInventory"),
        ("badges.request", Direction.Out, "GetBadges"),
        ("trade.confirmation", Direction.In, "TradingConfirmation"),
        ("trade.completed", Direction.In, "TradingCompleted"),
        ("polls.error", Direction.In, "PollError"),
        ("quests.request", Direction.Out, "GetQuests"),
        ("quests.seasonal.request", Direction.Out, "GetSeasonalQuestsOnly"),
        ("gifts.new_user.incomplete", Direction.In, "NewUserExperienceNotComplete"),
        ("wired.configuration.save.succeeded", Direction.In, "WiredSaveSuccess"),
        ("daily_tasks.request", Direction.Out, "GetDailyTasks"),
        ("habbicons.shop.request", Direction.Out, "GetHabbiconShopData"),
        ("achievements.request", Direction.Out, "GetAchievements"),
        ("achievement.point_limits.request", Direction.Out, "GetBadgePointLimits"),
        ("room.typing.start", Direction.Out, "StartTyping"),
        ("gifts.receiver.not_found", Direction.In, "GiftReceiverNotFound")
    ];

    [Fact]
    public void ClientHeadersIsAStandaloneLeaf()
    {
        XDocument project = XDocument.Load(ClientHeaders);

        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.DoesNotContain(
            project.Descendants("Reference"),
            reference => string.Equals(
                reference.Attribute("Include")?.Value,
                "Iced",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientHeaderDependencyClosureContainsNoAnalyzer()
    {
        string[] forbidden_projects = ["Flazzy", "QX.Swf", "QX.Unity"];
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Queue<string> pending = new([ClientHeaders]);
        var violations = new List<string>();

        while (pending.TryDequeue(out string? project_path))
        {
            project_path = Path.GetFullPath(project_path);
            if (!visited.Add(project_path))
                continue;

            XDocument project = XDocument.Load(project_path);
            string name = Path.GetFileNameWithoutExtension(project_path);
            if (forbidden_projects.Contains(name, StringComparer.OrdinalIgnoreCase))
                violations.Add(name);
            foreach (XElement package in project.Descendants("PackageReference"))
            {
                if (string.Equals(package.Attribute("Include")?.Value, "Iced", StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{name} -> Iced");
            }
            foreach (XElement reference in project.Descendants("ProjectReference"))
            {
                string include = reference.Attribute("Include")?.Value
                    ?? throw new InvalidDataException($"Project reference in '{project_path}' has no Include value.");
                pending.Enqueue(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project_path)!, include)));
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void HeaderDatabaseKeepsItsPublicResourceIdentity()
    {
        XElement resource = Assert.Single(XDocument.Load(ClientHeaders).Descendants("EmbeddedResource"));

        Assert.Equal("headers.json", resource.Attribute("Include")?.Value);
        Assert.Equal("QX.Unity.headers.json", resource.Attribute("LogicalName")?.Value);
    }

    [Theory]
    [InlineData(1676, "ItemsChestContentsUpdated")]
    [InlineData(1677, "ItemsChestContentsChunk")]
    public void UnityChestHeadersHaveFirstPartySemanticAliases(short id, string expected)
    {
        Qx.Unity.UnityHeaderNames names = Assert.IsType<Qx.Unity.UnityHeaderNames>(
            Qx.Unity.UnityHeaderNameDatabase.LoadDefault().Find(
                Qx.Unity.UnityHeaderDirection.Incoming,
                id));

        Assert.Null(names.Name);
        Assert.Equal(expected, names.FlashName);
    }

    [Theory]
    [InlineData("ShoutMessageEvent", true, "Shout")]
    [InlineData("WhisperMessageEvent", true, "Whisper")]
    [InlineData("ForumsListMessageEvent", true, "ForumsList")]
    [InlineData("GetForumsListMessageComposer", false, "GetForumsList")]
    [InlineData("ChatParser", true, null)]
    [InlineData("_-11P", true, null)]
    public void FlashConstructorNamesProvideFirstPartySemantics(
        string constructor_name,
        bool incoming,
        string? expected)
    {
        MethodInfo method = Assert.Single(
            typeof(FlashHeaderNameResolver).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            candidate => candidate.Name == "ConstructorName" &&
                candidate.GetParameters()[0].ParameterType == typeof(string));

        string? resolved = (string?)method.Invoke(null, [constructor_name, incoming]);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void PrimaryFlashSemanticsDisambiguateSharedParsers()
    {
        var catalog = new MessageCatalog();
        catalog.Add(Direction.In, 2294, "Shout");
        catalog.AddAlias(Direction.In, 2294, "Chat");
        catalog.Add(Direction.In, 2400, "Whisper");
        catalog.AddAlias(Direction.In, 2400, "Chat");
        catalog.Add(Direction.In, 2433, "Chat");
        catalog.Add(Direction.In, 2600, "ForumsList");
        catalog.AddAlias(Direction.In, 2600, "GetForumsList");
        MessageManager messages = MessageManager.CreateWithEmbeddedMap();
        messages.BindSessionCatalog(new SessionCatalogBinding(
            ClientType.Flash,
            catalog,
            new CatalogProvenance(
                CatalogOrigin.GEarthHandshake,
                ClientType.Flash,
                "test",
                "WIN63-test")));

        AssertHeader("room.chat.shout", 2294);
        AssertHeader("room.chat.whisper", 2400);
        AssertHeader("room.chat.talk", 2433);
        AssertHeader("forums.list", 2600);

        void AssertHeader(string key, int expected)
        {
            Assert.True(messages.TryGetHeader(ClientType.Flash, new MessageKey(key), out Header header));
            Assert.Equal(Direction.In, header.Direction);
            Assert.Equal(unchecked((short)expected), header.Value);
        }
    }

    [Fact]
    public void FlashCatalogKeepsTheOfficialSemanticAsPrimaryName()
    {
        var definition = new FlashHeaderDefinition
        {
            Id = 2294,
            Direction = MessageDirection.Incoming,
            Class = "_-11P",
            Namespace = "_-N22",
            Name = "Shout"
        };
        typeof(FlashHeaderDefinition)
            .GetProperty(nameof(FlashHeaderDefinition.SemanticAliases))!
            .SetValue(definition, new[] { "Chat" });
        Type extractor = typeof(HeaderCatalogSnapshot).Assembly.GetType(
            "Qx.ClientCatalog.HeaderCatalogExtractor",
            throwOnError: true)!;
        MethodInfo method = Assert.Single(
            extractor.GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            candidate => candidate.Name == "FlashEntry");

        HeaderCatalogEntry entry = Assert.IsType<HeaderCatalogEntry>(
            method.Invoke(null, [Direction.In, definition]));

        Assert.Equal("Shout", entry.Name);
        Assert.Contains("Chat", entry.Aliases);
        Assert.Contains("_-11P", entry.Aliases);
        Assert.Contains("_-N22._-11P", entry.Aliases);
    }

    [Theory]
    [InlineData("int,int,String,int", "int,int", "", FlashMarketplaceWireLayout.Legacy)]
    [InlineData("int,int,String,int,Boolean", "int,int,String", "int", FlashMarketplaceWireLayout.Modern)]
    [InlineData("int,int,String,int,Boolean", "int,int", "int", FlashMarketplaceWireLayout.Unknown)]
    [InlineData("int,int,String,int,uint", "int,int,String", "int", FlashMarketplaceWireLayout.Unknown)]
    public void FlashMarketplaceLayoutRequiresConsistentExactConstructors(
        string search_signature,
        string stats_signature,
        string own_offers_signature,
        FlashMarketplaceWireLayout expected)
    {
        using var map = new FlashHeaderMap();
        map.Outgoing.Add(FlashComposer(3118, "GetMarketplaceOffers", search_signature));
        map.Outgoing.Add(FlashComposer(1701, "GetMarketplaceItemStats", stats_signature));
        map.Outgoing.Add(FlashComposer(1702, "GetMarketplaceOwnOffers", own_offers_signature));
        Type detector = typeof(HeaderCatalogSnapshot).Assembly.GetType(
            "Qx.ClientCatalog.FlashMarketplaceLayoutDetector",
            throwOnError: true)!;
        MethodInfo method = Assert.Single(
            detector.GetMethods(BindingFlags.Static | BindingFlags.Public),
            candidate => candidate.Name == "Detect");

        FlashMarketplaceWireLayout layout = Assert.IsType<FlashMarketplaceWireLayout>(
            method.Invoke(null, [map]));

        Assert.Equal(expected, layout);
    }

    [Fact]
    public void FlashMarketplaceLayoutRejectsAnUnresolvedEmptyConstructor()
    {
        using var map = new FlashHeaderMap();
        map.Outgoing.Add(FlashComposer(3118, "GetMarketplaceOffers", "int,int,String,int"));
        map.Outgoing.Add(FlashComposer(1701, "GetMarketplaceItemStats", "int,int"));
        map.Outgoing.Add(FlashComposer(1702, "GetMarketplaceOwnOffers", "", false));
        Type detector = typeof(HeaderCatalogSnapshot).Assembly.GetType(
            "Qx.ClientCatalog.FlashMarketplaceLayoutDetector",
            throwOnError: true)!;
        MethodInfo method = Assert.Single(
            detector.GetMethods(BindingFlags.Static | BindingFlags.Public),
            candidate => candidate.Name == "Detect");

        FlashMarketplaceWireLayout layout = Assert.IsType<FlashMarketplaceWireLayout>(
            method.Invoke(null, [map]));

        Assert.Equal(FlashMarketplaceWireLayout.Unknown, layout);
    }

    [Fact]
    public async Task PreparedFlashCatalogPersistsItsMarketplaceLayout()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "QX.Tests",
            $"flash-marketplace-{Guid.NewGuid():N}");
        try
        {
            var provenance = new HeaderCatalogProvenance("10", "Launcher");
            var key = new HeaderCatalogKey(
                ClientType.Flash,
                new string('a', 64),
                new string('b', 64),
                "flash-fast-header-v6",
                provenance);
            var snapshot = new HeaderCatalogSnapshot(
                provenance,
                [
                    new HeaderCatalogEntry(
                        Direction.Out,
                        3118,
                        "GetMarketplaceOffers")
                ],
                ["WIN63-test"],
                FlashMarketplaceWireLayout.Modern);
            var first_store = new HeaderCatalogStore(root);
            HeaderCatalogCacheResult created = await first_store.GetOrCreateAsync(
                key,
                _ => Task.FromResult(snapshot),
                CancellationToken.None);
            var second_store = new HeaderCatalogStore(root);
            HeaderCatalogCacheResult loaded = await second_store.GetOrCreateAsync(
                key,
                _ => Task.FromException<HeaderCatalogSnapshot>(
                    new InvalidOperationException("The cached catalog was not loaded.")),
                CancellationToken.None);

            Assert.Equal(HeaderCatalogCacheState.Created, created.State);
            Assert.Equal(HeaderCatalogCacheState.Hit, loaded.State);
            Assert.Equal(
                FlashMarketplaceWireLayout.Modern,
                loaded.Catalog.FlashMarketplaceLayout);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PreparedFlashCatalogActivatesTheMarketplaceProfile()
    {
        PreparedHeaderCatalog prepared = FlashCatalog(
            FlashMarketplaceWireLayout.Modern);

        MessageWireProfile profile = ClientCatalogFactory.Create(prepared).WireProfile;

        Assert.True(profile.IsAnalyzed);
        Assert.Equal(
            FlashMarketplaceWireLayout.Modern,
            profile.FlashMarketplaceLayout);
    }

    [Theory]
    [InlineData(FlashMarketplaceWireLayout.Legacy, true)]
    [InlineData(FlashMarketplaceWireLayout.Modern, true)]
    [InlineData(FlashMarketplaceWireLayout.Unknown, false)]
    public void PreparedFlashMarketplaceProfileControlsSearchCapability(
        FlashMarketplaceWireLayout marketplace_layout,
        bool expected)
    {
        PreparedHeaderCatalog prepared = FlashCatalog(marketplace_layout);
        MessageCatalog catalog = ClientCatalogFactory.Create(prepared);
        MessageManager messages = MessageManager.CreateWithEmbeddedMap();
        messages.BindSessionCatalog(new SessionCatalogBinding(
            ClientType.Flash,
            catalog,
            new CatalogProvenance(
                CatalogOrigin.ClientExtraction,
                ClientType.Flash,
                "Launcher",
                "WIN63-test",
                prepared.Key.SourceSha256)));

        MessageDialectCapability capability = MessageContracts.Marketplace.Offers.SearchRequest.Capability(
            ClientType.Flash,
            messages,
            new Header(Direction.Out, 3118));

        Assert.Equal(expected, capability.Available);
    }

    [Fact]
    public void InstalledCandidateValidationUsesOnlyTheClientHeaderLeaf()
    {
        string source = File.ReadAllText(Path.Combine(
            Repository,
            "src",
            "QX.ClientCatalog",
            "InstalledClients",
            "InstalledClientDiscovery.cs"));

        Assert.Contains("UnityExecutableValidator.Validate", source, StringComparison.Ordinal);
        Assert.Contains("UnityHeaderExtractor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityPeCodeCatalogReader", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Iced", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeClientCatalogDependencyClosureContainsNoUnityAnalyzer()
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Queue<string> pending = new([ClientCatalog]);
        var violations = new List<string>();

        while (pending.TryDequeue(out string? project_path))
        {
            project_path = Path.GetFullPath(project_path);
            if (!visited.Add(project_path))
                continue;

            string name = Path.GetFileNameWithoutExtension(project_path);
            if (name.Equals("QX.Unity", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("QX.ClientCatalog.Analysis", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(name);
            }
            foreach (XElement reference in XDocument.Load(project_path).Descendants("ProjectReference"))
            {
                string include = reference.Attribute("Include")?.Value
                    ?? throw new InvalidDataException($"Project reference in '{project_path}' has no Include value.");
                pending.Enqueue(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project_path)!, include)));
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void CurrentUnityCatalogsCarryTheCompatibleWireFamily()
    {
        PreparedHeaderCatalog current = UnityCatalog("2455", 'A', 137, 100);
        PreparedHeaderCatalog legacy = UnityCatalog("2414", 'B', 137, 100);

        MessageWireProfile profile = ClientCatalogFactory.Create(current).WireProfile;

        Assert.True(profile.IsAnalyzed);
        Assert.True(profile.HasExactUnityIncomingLayout);
        Assert.Equal(MessageWiredContextLayout.Full, profile.WiredContextLayout);
        Assert.True(profile.WiredConditionHasSeparateInvert);
        Assert.True(profile.UnityAvatarStatusHasTargetId);
        Assert.True(profile.UnityUpdateAvatarHasBadgeRank);
        Assert.True(profile.UnityInventoryItemHasExtendedMetadata);
        Assert.True(profile.UnityGuestRoomResultHasExtendedData);
        Assert.True(profile.UnityCraftingProductHasProductCode);
        Assert.Equal(MarketplaceBuyWireLayout.OfferId, profile.UnityMarketplaceBuyLayout);
        Assert.Equal((short)3014, profile.UnityMarketplaceBuyHeaderId);
        Assert.Equal(ConsoleMessageWireLayout.TaggedHabbicon, profile.UnityConsoleMessageLayout);
        Assert.Equal(UnityRoomSettingsWireLayout.Modern, profile.UnityRoomSettingsLayout);
        Assert.False(ClientCatalogFactory.Create(legacy).WireProfile.IsAnalyzed);
    }

    [Fact]
    public void Unity21SelectsTheBestCompatiblePreparedCatalog()
    {
        PreparedHeaderCatalog older = UnityCatalog("2431", 'A', 1379, 1196);
        PreparedHeaderCatalog current = UnityCatalog("2455", 'B', 1379, 1196);
        PreparedHeaderCatalog incompatible = UnityCatalog("2460", 'C', 1379, 1000);
        PreparedHeaderCatalog[] catalogs = [older, current, incompatible];
        ConstructorInfo constructor = Assert.Single(
            typeof(PreparedSessionCatalogSelector).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        var selector = (PreparedSessionCatalogSelector)constructor.Invoke(
        [
            new Func<ClientType, IReadOnlyList<PreparedHeaderCatalog>>(
                client => client == ClientType.Unity ? catalogs : []),
            null
        ]);
        var fallback = new MessageCatalog();
        for (int id = 1; id <= 1196; id++)
            fallback.Add(Direction.In, id, $"Known{id}");
        for (int id = 1197; id <= 1232; id++)
            fallback.Add(Direction.In, id, ObfuscatedName('B', id));
        for (int id = 2000; id <= 2059; id++)
            fallback.Add(Direction.In, id, $"Stale{id}");
        var fallback_binding = new SessionCatalogBinding(
            ClientType.Unity,
            fallback,
            new CatalogProvenance(
                CatalogOrigin.GEarthHandshake,
                ClientType.Unity,
                "G-Earth",
                "UNITY21"));

        SessionCatalogBinding selected = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Unity,
                "UNITY21",
                "UNITY1",
                fallback_binding)));
        SessionCatalogBinding refreshed = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Unity,
                "UNITY21",
                "UNITY1",
                fallback_binding,
                SessionCatalogSelectionIntent.CatalogReady)));

        Assert.Equal(CatalogOrigin.ClientExtraction, selected.Provenance.Origin);
        Assert.Equal("2455", selected.Provenance.ClientVersion);
        Assert.Equal("2455", refreshed.Provenance.ClientVersion);
        Assert.Equal(1380, selected.Catalog!.HeaderCount);
    }

    [Fact]
    public void FlashSelectionEnrichesCurrentSemanticGapsWithoutReplacingTheQxCatalog()
    {
        const string build_id = "WIN63-202609021434-290314188";
        HeaderCatalogEntry[] entries = FlashEntriesWithSemanticGaps();
        PreparedHeaderCatalog prepared = FlashCatalog(entries, build_id, 'd');
        PreparedSessionCatalogSelector selector = CatalogSelector(prepared);
        MessageCatalog fallback = FlashFallback(
            entries,
            FlashSemanticGaps
                .Select((gap, index) => (gap.Direction, index + 1, gap.FlashName))
                .ToArray());
        MessageCatalogHeader[] fallback_headers = fallback.Headers.ToArray();
        int fallback_count = fallback.Count;
        SessionCatalogBinding fallback_binding = FlashFallbackBinding(fallback, build_id);

        SessionCatalogBinding selected = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                fallback_binding)));

        MessageCatalog catalog = Assert.IsType<MessageCatalog>(selected.Catalog);
        Assert.Equal(CatalogOrigin.ClientExtraction, selected.Provenance.Origin);
        Assert.Equal(build_id, selected.Provenance.ClientVersion);
        Assert.Equal(prepared.Key.SourceSha256.ToUpperInvariant(), selected.Provenance.SourceSha256);
        Assert.True(catalog.MatchesBuildFingerprint(prepared.Key.SourceSha256));
        Assert.Equal(entries.Length, catalog.HeaderCount);
        Assert.Equal(FlashMarketplaceWireLayout.Modern, catalog.WireProfile.FlashMarketplaceLayout);
        CatalogSupplement supplement = Assert.IsType<CatalogSupplement>(selected.Supplement);
        Assert.Equal(CatalogOrigin.GEarthHandshake, supplement.Provenance.Origin);
        Assert.Equal(build_id, supplement.Provenance.ClientVersion);
        Assert.Equal(FlashSemanticGaps.Length, supplement.AliasCount);
        Assert.Equal(fallback_headers, fallback.Headers);
        Assert.Equal(fallback_count, fallback.Count);

        var messages = MessageManager.CreateWithEmbeddedMap();
        messages.BindSessionCatalog(selected);
        for (int index = 0; index < FlashSemanticGaps.Length; index++)
        {
            (string key, Direction direction, string flash_name) = FlashSemanticGaps[index];
            short expected_id = checked((short)(index + 1));
            Assert.True(catalog.TryGetIds(direction, flash_name, out IReadOnlyList<short> ids), key);
            Assert.Equal([expected_id], ids);
            Assert.True(catalog.TryGetName(direction, expected_id, out string primary), key);
            Assert.Equal(FlashObfuscatedName(index + 1), primary);
            Assert.True(messages.TryGetHeader(ClientType.Flash, new MessageKey(key), out Header header), key);
            Assert.Equal(new Header(direction, expected_id), header);
            HeaderCatalogEntry source = Assert.Single(prepared.Catalog.Entries, entry =>
                entry.Direction == direction &&
                entry.HeaderId == index + 1);
            Assert.Equal(FlashObfuscatedName(index + 1), source.Name);
            Assert.DoesNotContain(flash_name, source.Aliases, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FlashSelectionAddsOnlyUnambiguousAliasesToObfuscatedHeaders()
    {
        const string build_id = "WIN63-current";
        HeaderCatalogEntry[] entries = Enumerable.Range(1, 100)
            .Select(id => new HeaderCatalogEntry(
                Direction.Out,
                checked((ushort)id),
                id switch
                {
                    95 => FlashObfuscatedName(id),
                    96 => $"§_-gap{id}",
                    97 => "GetPetInventory",
                    98 => "RequestFurniInventory",
                    99 => FlashObfuscatedName(id),
                    100 => "FriendlyName",
                    _ => $"Stable{id}"
                }))
            .ToArray();
        PreparedSessionCatalogSelector selector = CatalogSelector(
            FlashCatalog(entries, build_id, 'e'));
        var fallback = new MessageCatalog();
        foreach (HeaderCatalogEntry entry in entries)
        {
            string name = entry.HeaderId switch
            {
                95 => "ArbitraryStableName",
                96 => "StartTyping",
                97 => "GetBadges",
                98 or 99 => "RequestFurniInventory",
                100 => "Quit",
                _ => entry.Name!
            };
            fallback.Add(entry.Direction, entry.HeaderId, name);
        }

        SessionCatalogBinding selected = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                FlashFallbackBinding(fallback, build_id))));
        MessageCatalog catalog = Assert.IsType<MessageCatalog>(selected.Catalog);

        Assert.False(catalog.TryGetId(Direction.Out, "ArbitraryStableName", out _));
        Assert.True(catalog.TryGetId(Direction.Out, "StartTyping", out short typing));
        Assert.Equal((short)96, typing);
        Assert.True(catalog.TryGetName(Direction.Out, 96, out string primary));
        Assert.Equal("§_-gap96", primary);
        Assert.True(catalog.TryGetId(Direction.Out, "GetPetInventory", out short pets));
        Assert.Equal((short)97, pets);
        Assert.False(catalog.TryGetId(Direction.Out, "GetBadges", out _));
        Assert.True(catalog.TryGetIds(
            Direction.Out,
            "RequestFurniInventory",
            out IReadOnlyList<short> inventory));
        Assert.Equal([(short)98], inventory);
        Assert.False(catalog.TryGetId(Direction.Out, "Quit", out _));
        Assert.Equal(entries.Length, catalog.HeaderCount);
        Assert.Equal(CatalogOrigin.ClientExtraction, selected.Provenance.Origin);
        Assert.Equal(1, Assert.IsType<CatalogSupplement>(selected.Supplement).AliasCount);
        Assert.Equal("ArbitraryStableName", Assert.Single(
            fallback.Headers,
            header => header.Id == 95).Name);
        Assert.Equal("RequestFurniInventory", Assert.Single(
            fallback.Headers,
            header => header.Id == 99).Name);
    }

    [Fact]
    public void FlashSelectionRejectsMismatchedIncompleteAndForeignFallbacksWithoutLeakingAliases()
    {
        const string build_id = "WIN63-current";
        HeaderCatalogEntry[] entries = Enumerable.Range(1, 100)
            .Select(id => new HeaderCatalogEntry(
                Direction.Out,
                checked((ushort)id),
                id == 100 ? FlashObfuscatedName(id) : $"Stable{id}"))
            .ToArray();
        PreparedSessionCatalogSelector selector = CatalogSelector(
            FlashCatalog(entries, build_id, 'f'));
        MessageCatalog complete = FlashFallback(entries, (Direction.Out, 100, "StartTyping"));
        MessageCatalogHeader[] complete_headers = complete.Headers.ToArray();
        int complete_count = complete.Count;

        SessionCatalogBinding enriched = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                FlashFallbackBinding(complete, build_id))));
        Assert.True(enriched.Catalog!.TryGetId(Direction.Out, "StartTyping", out short enriched_id));
        Assert.Equal((short)100, enriched_id);

        SessionCatalogBinding mismatched = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                FlashFallbackBinding(complete, "WIN63-other"))));

        var incomplete = new MessageCatalog();
        for (int id = 1; id <= 93; id++)
            incomplete.Add(Direction.Out, id, $"Stable{id}");
        incomplete.Add(Direction.Out, 100, "StartTyping");
        SessionCatalogBinding too_small = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                FlashFallbackBinding(incomplete, build_id))));

        var foreign = new MessageCatalog();
        for (int id = 1; id <= 93; id++)
            foreign.Add(Direction.Out, id, $"Stable{id}");
        foreign.Add(Direction.Out, 100, "StartTyping");
        foreign.Add(Direction.Out, 101, "GetBadges");
        SessionCatalogBinding outside_qx = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                FlashFallbackBinding(foreign, build_id))));

        SessionCatalogBinding wrong_origin = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                new SessionCatalogBinding(
                    ClientType.Flash,
                    complete,
                    new CatalogProvenance(
                        CatalogOrigin.Sulek,
                        ClientType.Flash,
                        "Sulek",
                        build_id)))));

        SessionCatalogBinding wrong_client = Assert.IsType<SessionCatalogBinding>(selector.Select(
            new SessionCatalogRequest(
                ClientType.Flash,
                build_id,
                "FLASH",
                new SessionCatalogBinding(
                    ClientType.Unity,
                    complete,
                    new CatalogProvenance(
                        CatalogOrigin.GEarthHandshake,
                        ClientType.Unity,
                        "G-Earth",
                        build_id)))));

        Assert.False(mismatched.Catalog!.TryGetId(Direction.Out, "StartTyping", out _));
        Assert.False(too_small.Catalog!.TryGetId(Direction.Out, "StartTyping", out _));
        Assert.False(outside_qx.Catalog!.TryGetId(Direction.Out, "StartTyping", out _));
        Assert.False(wrong_origin.Catalog!.TryGetId(Direction.Out, "StartTyping", out _));
        Assert.False(wrong_client.Catalog!.TryGetId(Direction.Out, "StartTyping", out _));
        Assert.Equal(CatalogOrigin.ClientExtraction, mismatched.Provenance.Origin);
        Assert.Equal(CatalogOrigin.ClientExtraction, too_small.Provenance.Origin);
        Assert.Equal(CatalogOrigin.ClientExtraction, outside_qx.Provenance.Origin);
        Assert.Equal(CatalogOrigin.ClientExtraction, wrong_origin.Provenance.Origin);
        Assert.Equal(CatalogOrigin.ClientExtraction, wrong_client.Provenance.Origin);
        Assert.Equal(entries.Length, mismatched.Catalog.HeaderCount);
        Assert.Equal(entries.Length, too_small.Catalog.HeaderCount);
        Assert.Equal(entries.Length, outside_qx.Catalog.HeaderCount);
        Assert.Equal(entries.Length, wrong_origin.Catalog.HeaderCount);
        Assert.Equal(entries.Length, wrong_client.Catalog.HeaderCount);
        Assert.Null(mismatched.Supplement);
        Assert.Null(too_small.Supplement);
        Assert.Null(outside_qx.Supplement);
        Assert.Null(wrong_origin.Supplement);
        Assert.Null(wrong_client.Supplement);
        Assert.True(complete.TryGetId(Direction.Out, "StartTyping", out short fallback_id));
        Assert.Equal((short)100, fallback_id);
        Assert.Equal(complete_headers, complete.Headers);
        Assert.Equal(complete_count, complete.Count);
    }

    [Fact]
    public void DeepUnityAnalysisOverridesOnlyTheLayoutsItResolved()
    {
        PreparedHeaderCatalog prepared = UnityCatalog("2455", 'D', 137, 100);
        MessageCatalog compatible = ClientCatalogFactory.Create(prepared);
        var analyzed = new MessageCatalog();
        foreach (MessageCatalogHeader header in compatible.Headers)
            analyzed.Add(header.Direction, header.Id, header.Name);
        analyzed.SetBuildFingerprint(prepared.Key.SourceSha256);
        analyzed.SetSchemaFingerprint("schema");
        analyzed.SetWireProfile(new MessageWireProfile(
            MessageWiredContextLayout.Unknown,
            null,
            UnityAvatarStatusHasTargetId: false));
        var messages = MessageManager.CreateWithEmbeddedMap();
        messages.LoadVerifiedFallbackCatalog(ClientType.Unity, analyzed, preferred: false);
        messages.BindCatalogBuild(
            ClientType.Unity,
            new ClientBuildIdentity(prepared.Key.SourceSha256, "schema"));
        messages.BindSessionCatalog(new SessionCatalogBinding(
            ClientType.Unity,
            compatible,
            new CatalogProvenance(
                CatalogOrigin.ClientExtraction,
                ClientType.Unity,
                prepared.SourcePath,
                prepared.Candidate.Version,
                prepared.Key.SourceSha256)));

        MessageWireProfile profile = messages.GetWireProfile(ClientType.Unity);

        Assert.False(profile.UnityAvatarStatusHasTargetId);
        Assert.True(profile.UnityInventoryItemHasExtendedMetadata);
        Assert.Equal(UnityRoomSettingsWireLayout.Modern, profile.UnityRoomSettingsLayout);
    }

    [Fact]
    public void UnityReferenceResolvesTheRoutesMissingFromTheGEarthSubset()
    {
        string[] keys =
        [
            "room.access.open.request",
            "room.floor_item.removed_multiple",
            "users.block.list.snapshot",
            "users.block.updated",
            "inventory.furni.request",
            "badges.request",
            "quests.seasonal.request",
            "wired.environment",
            "wired.click_settings",
            "wired.configuration.selector",
            "wired.configuration.addon",
            "wired.user_click.result",
            "wired.transaction.succeeded",
            "wired.transaction.failed",
            "wired.trade.initiated",
            "wired.trade.items.updated",
            "wired.trade.cancelled",
            "wired.trade.completed",
            "wired.trade.notification",
            "habbicons.shop.snapshot",
            "habbicons.inventory.snapshot",
            "habbicon.status.updated",
            "habbicon.info.snapshot",
            "habbicon.room.used",
            "earnings.status.request",
            "earnings.claim",
            "achievements.request",
            "room.movement.walk",
            "room.typing.start",
            "room.floor_item.use"
        ];
        MessageCatalog reference = ClientCatalogFactory.CreateUnityReference();
        HeaderCatalogEntry[] entries =
        [
            .. reference.Headers.Select(header => new HeaderCatalogEntry(
                header.Direction,
                checked((ushort)header.Id),
                header.Name))
        ];
        PreparedHeaderCatalog prepared = UnityCatalog("2455", 'A', entries);
        var messages = MessageManager.CreateWithEmbeddedMap();
        messages.BindSessionCatalog(new SessionCatalogBinding(
            ClientType.Unity,
            ClientCatalogFactory.Create(prepared),
            new CatalogProvenance(
                CatalogOrigin.ClientExtraction,
                ClientType.Unity,
                prepared.SourcePath,
                prepared.Candidate.Version,
                prepared.Key.SourceSha256)));

        Assert.All(keys, key => Assert.True(messages.HasMessage(new MessageKey(key)), key));
    }

    [Fact]
    public async Task SessionWaitsForQxCatalogAndDispatchesQueuedPacketsAfterConnected()
    {
        MessageManager messages = MessageManager.CreateWithEmbeddedMap();
        var readiness = new PendingCatalogReadiness();
        var selector = new FixedCatalogSelector(ExtractedCatalogBinding());
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var extension = new GEarthExtension(new GEarthOptions { Port = port }, messages)
        {
            CatalogReadiness = readiness,
            SessionCatalogSelector = selector
        };
        var order = new List<string>();
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var intercepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        extension.Connected += _ =>
        {
            order.Add("connected");
            connected.TrySetResult();
        };
        using IDisposable subscription = extension.Intercept(
            new Header(Direction.In, 77),
            _ =>
            {
                order.Add("packet");
                intercepted.TrySetResult();
            });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task run = extension.RunAsync(cancellation.Token);
        using TcpClient server = await listener.AcceptTcpClientAsync(cancellation.Token);

        try
        {
            byte[] connection_start = ConnectionStartFrame();
            byte[] packet = InterceptFrame();
            byte[] frames = [.. connection_start, .. packet];
            await server.GetStream().WriteAsync(frames, cancellation.Token);

            await readiness.Entered.WaitAsync(cancellation.Token);
            Assert.Null(extension.Session);
            Assert.Null(messages.ActiveCatalogBinding);
            Assert.False(connected.Task.IsCompleted);
            Assert.False(intercepted.Task.IsCompleted);

            readiness.Complete();
            await connected.Task.WaitAsync(cancellation.Token);
            await intercepted.Task.WaitAsync(cancellation.Token);

            SessionCatalogBinding binding = Assert.IsType<SessionCatalogBinding>(
                messages.ActiveCatalogBinding);
            Assert.Equal(CatalogOrigin.ClientExtraction, binding.Provenance.Origin);
            Assert.Equal(["connected", "packet"], order);
        }
        finally
        {
            readiness.Complete();
            cancellation.Cancel();
            server.Dispose();
            listener.Stop();
            try
            {
                await run;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task FailedQxPreparationBindsFallbackOnceForTheWholeSession()
    {
        MessageManager messages = MessageManager.CreateWithEmbeddedMap();
        var selector = new FixedCatalogSelector(null);
        using var extension = new GEarthExtension(new GEarthOptions(), messages)
        {
            CatalogReadiness = new FailingCatalogReadiness(),
            SessionCatalogSelector = selector
        };
        MethodInfo start = Assert.Single(typeof(GEarthExtension).GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic),
            method => method.Name == "StartConnectionAsync");
        byte[] frame = ConnectionStartFrame();

        await Assert.IsAssignableFrom<Task>(start.Invoke(
            extension,
            [new ReadOnlyMemory<byte>(frame, 6, frame.Length - 6), CancellationToken.None]));

        SessionCatalogBinding fallback = Assert.IsType<SessionCatalogBinding>(
            messages.ActiveCatalogBinding);
        Assert.Equal(CatalogOrigin.GEarthHandshake, fallback.Provenance.Origin);
        selector.Binding = ExtractedCatalogBinding();
        extension.CatalogReadiness = new CompletedCatalogReadiness();

        await extension.WaitForCatalogBuildAsync();

        Assert.Same(fallback, messages.ActiveCatalogBinding);
        Assert.Equal(CatalogOrigin.GEarthHandshake, messages.ActiveCatalogBinding!.Provenance.Origin);
    }

    static byte[] ConnectionStartFrame()
    {
        var writer = new GControlWriter(GControl.Outgoing.ConnectionStart);
        writer.WriteString("game.habbo.test");
        writer.WriteInt(3000);
        writer.WriteString("UNITY21");
        writer.WriteString("UNITY1");
        writer.WriteString("UNITY");
        writer.WriteInt(1);
        writer.WriteInt(77);
        writer.WriteString("");
        writer.WriteString("FallbackPacket");
        writer.WriteString("");
        writer.WriteBool(false);
        writer.WriteString("");
        return writer.ToFrame();
    }

    static byte[] InterceptFrame()
    {
        string message;
        using (var packet = new Packet(new Header(Direction.In, 77), ClientType.Unity))
        {
            message = new HMessage
            {
                Direction = Direction.In,
                Index = 1,
                IsBlocked = false,
                IsEdited = false,
                Packet = packet
            }.Stringify();
        }
        var writer = new GControlWriter(GControl.Outgoing.PacketIntercept);
        writer.WriteLongString(message);
        writer.WriteInt(0);
        return writer.ToFrame();
    }

    static SessionCatalogBinding ExtractedCatalogBinding()
    {
        const string fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var catalog = new MessageCatalog();
        catalog.Add(Direction.In, 77, "QxPacket");
        catalog.SetBuildFingerprint(fingerprint);
        return new SessionCatalogBinding(
            ClientType.Unity,
            catalog,
            new CatalogProvenance(
                CatalogOrigin.ClientExtraction,
                ClientType.Unity,
                "test",
                "2455",
                fingerprint));
    }

    static PreparedHeaderCatalog UnityCatalog(
        string version,
        char identity,
        int header_count,
        int matching_count)
    {
        HeaderCatalogEntry[] entries =
        [
            .. Enumerable.Range(1, header_count).Select(id => new HeaderCatalogEntry(
                Direction.In,
                checked((ushort)id),
                id <= matching_count ? $"Known{id}" : ObfuscatedName(identity, id))),
            new HeaderCatalogEntry(
                Direction.Out,
                3014,
                "MarketplaceBuyOffer",
                ["BuyMarketplaceOffer"])
        ];
        return UnityCatalog(version, identity, entries);
    }

    sealed class CompletedCatalogReadiness : IMessageCatalogReadiness
    {
        public Task WaitUntilReadyAsync(CancellationToken cancellation_token = default) =>
            Task.CompletedTask;
    }

    sealed class PendingCatalogReadiness : IMessageCatalogReadiness
    {
        readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async Task WaitUntilReadyAsync(CancellationToken cancellation_token = default)
        {
            _entered.TrySetResult();
            await _completed.Task.WaitAsync(cancellation_token);
        }

        public void Complete() => _completed.TrySetResult();
    }

    sealed class FailingCatalogReadiness : IMessageCatalogReadiness
    {
        public Task WaitUntilReadyAsync(CancellationToken cancellation_token = default) =>
            Task.FromException(new InvalidDataException("extraction failed"));
    }

    sealed class FixedCatalogSelector(SessionCatalogBinding? binding) : ISessionCatalogSelector
    {
        public SessionCatalogBinding? Binding { get; set; } = binding;

        public SessionCatalogBinding? Select(SessionCatalogRequest request) => Binding;
    }

    static PreparedSessionCatalogSelector CatalogSelector(params PreparedHeaderCatalog[] catalogs)
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(PreparedSessionCatalogSelector).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic));
        return (PreparedSessionCatalogSelector)constructor.Invoke(
        [
            new Func<ClientType, IReadOnlyList<PreparedHeaderCatalog>>(
                client => client == ClientType.Flash ? catalogs : []),
            MessagesIniParser.ParseEmbeddedRegistry()
        ]);
    }

    static HeaderCatalogEntry[] FlashEntriesWithSemanticGaps()
    {
        var entries = FlashSemanticGaps
            .Select((gap, index) => new HeaderCatalogEntry(
                gap.Direction,
                checked((ushort)(index + 1)),
                FlashObfuscatedName(index + 1)))
            .ToList();
        entries.AddRange(Enumerable.Range(
                FlashSemanticGaps.Length + 1,
                100 - FlashSemanticGaps.Length)
            .Select(id => new HeaderCatalogEntry(
                Direction.Out,
                checked((ushort)id),
                $"Stable{id}")));
        return entries.ToArray();
    }

    static MessageCatalog FlashFallback(
        IReadOnlyList<HeaderCatalogEntry> entries,
        params (Direction Direction, int Id, string Name)[] replacements)
    {
        Dictionary<(Direction, ushort), string> names = replacements.ToDictionary(
            replacement => (replacement.Direction, checked((ushort)replacement.Id)),
            replacement => replacement.Name);
        var catalog = new MessageCatalog();
        foreach (HeaderCatalogEntry entry in entries)
        {
            string name = names.TryGetValue(
                (entry.Direction, entry.HeaderId),
                out string? replacement)
                ? replacement
                : entry.Name!;
            catalog.Add(entry.Direction, entry.HeaderId, name);
        }
        return catalog;
    }

    static SessionCatalogBinding FlashFallbackBinding(MessageCatalog catalog, string build_id) =>
        new(
            ClientType.Flash,
            catalog,
            new CatalogProvenance(
                CatalogOrigin.GEarthHandshake,
                ClientType.Flash,
                "G-Earth",
                build_id));

    static string FlashObfuscatedName(int id) => $"_-gap{id}";

    static PreparedHeaderCatalog UnityCatalog(
        string version,
        char identity,
        HeaderCatalogEntry[] entries)
    {
        string root = Path.Combine(Path.GetTempPath(), "QX.Tests", $"unity-{version}-{identity}");
        string source_path = Path.Combine(root, "global-metadata.dat");
        string source_hash = new(identity, 64);
        var provenance = new HeaderCatalogProvenance(version, "Launcher");
        var candidate = new InstalledClientCandidate(
            InstalledClientFamily.Unity,
            version,
            root,
            "Launcher",
            DateTimeOffset.UnixEpoch.AddSeconds(long.Parse(version)),
            [source_path]);
        var key = new HeaderCatalogKey(
            ClientType.Unity,
            source_hash,
            new string('a', 64),
            "unity-fast-header-v1",
            provenance);
        return new PreparedHeaderCatalog(
            candidate,
            root,
            source_path,
            key,
            new HeaderCatalogSnapshot(provenance, entries),
            HeaderCatalogCacheState.Created,
            new string('b', 64),
            DateTimeOffset.UnixEpoch.AddSeconds(long.Parse(version)));
    }

    static PreparedHeaderCatalog FlashCatalog(
        FlashMarketplaceWireLayout marketplace_layout)
    {
        string root = Path.Combine(Path.GetTempPath(), "QX.Tests", "flash-10");
        string source_path = Path.Combine(root, "HabboAir.swf");
        var provenance = new HeaderCatalogProvenance("10", "Launcher");
        var candidate = new InstalledClientCandidate(
            InstalledClientFamily.Flash,
            "10",
            root,
            "Launcher",
            DateTimeOffset.UnixEpoch,
            [source_path]);
        var key = new HeaderCatalogKey(
            ClientType.Flash,
            new string('a', 64),
            new string('b', 64),
            "flash-fast-header-v6",
            provenance);
        return new PreparedHeaderCatalog(
            candidate,
            root,
            source_path,
            key,
            new HeaderCatalogSnapshot(
                provenance,
                [
                    new HeaderCatalogEntry(
                        Direction.Out,
                        3118,
                        "GetMarketplaceOffers")
                ],
                ["WIN63-test"],
                marketplace_layout),
            HeaderCatalogCacheState.Created,
            new string('c', 64),
            DateTimeOffset.UnixEpoch);
    }

    static PreparedHeaderCatalog FlashCatalog(
        IReadOnlyList<HeaderCatalogEntry> entries,
        string build_id,
        char identity)
    {
        string root = Path.Combine(Path.GetTempPath(), "QX.Tests", $"flash-13-{identity}");
        string source_path = Path.Combine(root, "HabboAir.swf");
        var provenance = new HeaderCatalogProvenance("13", "Launcher");
        var candidate = new InstalledClientCandidate(
            InstalledClientFamily.Flash,
            "13",
            root,
            "Launcher",
            DateTimeOffset.UnixEpoch,
            [source_path]);
        var key = new HeaderCatalogKey(
            ClientType.Flash,
            new string(identity, 64),
            new string('a', 64),
            "flash-fast-header-v6",
            provenance);
        return new PreparedHeaderCatalog(
            candidate,
            root,
            source_path,
            key,
            new HeaderCatalogSnapshot(
                provenance,
                entries,
                [build_id],
                FlashMarketplaceWireLayout.Modern),
            HeaderCatalogCacheState.Created,
            new string('b', 64),
            DateTimeOffset.UnixEpoch);
    }

    static FlashHeaderDefinition FlashComposer(
        int id,
        string name,
        string signature,
        bool resolved = true)
    {
        var definition = new FlashHeaderDefinition
        {
            Id = id,
            Direction = MessageDirection.Outgoing,
            Class = $"_-{id}",
            Namespace = "_-marketplace",
            Name = name
        };
        string[] parameter_types = signature.Length == 0 ? [] : signature.Split(',');
        typeof(FlashHeaderDefinition)
            .GetProperty(nameof(FlashHeaderDefinition.ConstructorSignatureResolved))!
            .SetValue(definition, resolved);
        typeof(FlashHeaderDefinition)
            .GetProperty(nameof(FlashHeaderDefinition.ConstructorParameterTypes))!
            .SetValue(definition, parameter_types);
        return definition;
    }

    static string ObfuscatedName(char identity, int id)
    {
        Span<char> value = stackalloc char[24];
        value.Fill(identity);
        int remaining = id;
        for (int index = value.Length - 1; index >= 16; index--)
        {
            value[index] = (char)('A' + (remaining & 3));
            remaining >>= 2;
        }
        return new string(value);
    }

    static string FindRepository()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QX.slnx")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("QX repository was not found.");
    }
}
