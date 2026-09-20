using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Presentation.Services.FurniLookup;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Images;

namespace Qx.Presentation.Services.Inventory;

public static class InventoryRead
{
    public const int PageLimit = 500;

    public static async Task<InventoryContents> LoadAsync(IGameGateway gateway, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        (InventoryFurniPage furni, InventoryPetPage pets) = await gateway.ReadStableAsync(
            async token => (
                await InventoryApplicationPages.ReadFurniAsync(gateway.Application, cancellation_token: token),
                await InventoryApplicationPages.ReadPetsAsync(gateway.Application, cancellation_token: token)),
            cancellation_token);
        GameData data = gateway.Game.GameData;
        return Build(furni, pets, data.Furni, data.Texts);
    }

    public static InventoryContents Build(InventoryFurniPage furni, InventoryPetPage pets, FurniData? data, ExternalTexts? texts)
    {
        ArgumentNullException.ThrowIfNull(furni);
        ArgumentNullException.ThrowIfNull(pets);
        bool connected = furni.Connected && pets.Connected;
        bool consistent = connected &&
            furni.SessionGeneration == pets.SessionGeneration &&
            furni.Client == pets.Client &&
            furni.Revision == pets.Revision &&
            !furni.Stale &&
            !pets.Stale &&
            !furni.RecoveryPending &&
            !pets.RecoveryPending;
        if (!consistent)
            return new InventoryContents(connected, false, furni.Loaded, 0, 0, 0, []);

        List<InventoryEntry> kinds =
        [
            .. furni.Items
                .Select(item => (Item: item, Type: TypeOf(item.Type)))
                .Where(entry => entry.Type is not null)
                .GroupBy(entry => (Type: entry.Type!.Value, entry.Item.Kind))
                .Select(group => Furni(group.Key.Type, group.Key.Kind, [.. group.Select(entry => entry.Item)], data, texts))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
        ];
        List<InventoryEntry> animals =
        [
            .. pets.Pets
                .OrderBy(pet => pet.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(Pet)
        ];
        return new InventoryContents(true, true, furni.Loaded, furni.Total, kinds.Count, animals.Count, [.. kinds, .. animals]);
    }

    static InventoryEntry Furni(ItemType type, int kind, IReadOnlyList<InventoryItemSnapshot> copies, FurniData? data, ExternalTexts? texts)
    {
        FurniDefinitionSnapshot? definition = copies.Select(item => item.Definition).FirstOrDefault(value => value is not null);
        FurniInfo? info = data?.GetInfo(type, kind);
        string identifier = Pick(definition?.Identifier, info?.Identifier);
        string name = FurniText.Named(identifier, Pick(definition?.Name, info?.Name), key => texts?[key]);
        int revision = definition is { Revision: > 0 } ? definition.Revision : info?.Revision ?? 0;
        Id[] sellable = [.. copies.Where(item => item.IsTradeable && item.IsSellable).Select(item => item.ItemId)];
        return new InventoryEntry(
            new InventoryItemKind(type, kind),
            name.Length > 0 ? name : $"Furni {kind}",
            identifier,
            identifier,
            type == ItemType.Wall ? "wall" : "floor",
            type,
            copies.Count,
            sellable,
            sellable.Length > 0,
            copies[0].ItemId,
            HabboUrls.FurniIcon(revision, identifier));
    }

    static string Pick(string? first, string? second) =>
        first is { Length: > 0 } ? first : second ?? "";

    static InventoryEntry Pet(InventoryPetSnapshot pet) =>
        new(new InventoryItemKind(null, pet.Id),
            pet.Name,
            "",
            $"breed {pet.BreedId}, level {pet.Level}",
            "pet",
            null,
            1,
            [],
            false,
            pet.Id,
            null);

    static ItemType? TypeOf(string value) =>
        Enum.TryParse(value, false, out ItemType type) && type is ItemType.Floor or ItemType.Wall ? type : null;
}
