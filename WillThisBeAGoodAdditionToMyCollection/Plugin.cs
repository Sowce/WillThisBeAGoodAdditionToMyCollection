using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using WillThisBeAGoodAdditionToMyCollection.Windows;

namespace WillThisBeAGoodAdditionToMyCollection;

public unsafe sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private static readonly Lazy<FrozenDictionary<uint, uint>> CabinetLookup = new(()
        => DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Cabinet>()
            .Where(row => row.RowId >= 1048 && row.Item.RowId != 0)
            .ToFrozenDictionary(row => row.Item.RowId, row => row.RowId));

    private static readonly List<InventoryType> bags = new List<InventoryType>()
    {
        InventoryType.EquippedItems,
        InventoryType.SaddleBag1, InventoryType.SaddleBag2,
        InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2,
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
    };


    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("WillThisBeAGoodAdditionToMyCollection");
    private ConfigWindow ConfigWindow { get; init; }
    private LootOverlay LootOverlay { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        LootOverlay = new LootOverlay(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(LootOverlay);

        AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "NeedGreed", OnLoot);
        AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "NeedGreed", LootWindowUpdate);

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "NeedGreed", OnLoot);
        AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "NeedGreed", LootWindowUpdate);

        ConfigWindow.Dispose();
        LootOverlay.Dispose();
    }

    private void LootWindowUpdate(AddonEvent type, AddonArgs args)
    {
        LootOverlay.IsOpen = args.Addon.IsVisible;

        if (args.Addon.IsVisible)
        {
            LootOverlay.Position = args.Addon.Position;
            LootOverlay.Size = new Vector2(args.Addon.ScaledWidth, args.Addon.ScaledHeight);
        }
    }

    private void OnLoot(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonNeedGreed*)args.Addon.Address;
        var widgetScale = addon->Scale;

        var listComponentNode = (AtkComponentNode*)addon->AtkUnitBase.GetNodeById(6);
        if (listComponentNode is null)
        {
            return;
        }

        var atkListComponent = (AtkComponentList*)listComponentNode->Component;
        if (atkListComponent is null)
        {
            return;
        }

        LootOverlay.currentLoot.Clear();
        var ItemSheet = DataManager.Excel.GetSheet<Item>();

        foreach (var index in Enumerable.Range(0, addon->Items.Length))
        {
            ref var itemInfo = ref addon->Items[index];

            if (itemInfo.ItemId == 0)
            {
                continue;
            }

            var listItemRenderer = atkListComponent->GetItemRenderer(index);
            var uiNode = listItemRenderer->AtkResNode;
            var size = new Vector2(uiNode->Width, uiNode->Height) * widgetScale;
            var position = new Vector2(uiNode->ScreenX - addon->X, uiNode->ScreenY - addon->Y + size.Y);

            var adjustedItemId = itemInfo.ItemId > 1_000_000 ? itemInfo.ItemId - 1_000_000 : itemInfo.ItemId;

            if (!ItemSheet.TryGetRow(adjustedItemId, out var itemRow))
            {
                continue;
            }

#if DEBUG
            Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - Category: {itemRow.ItemUICategory.RowId}, ClassJobCategory: {itemRow.ClassJobCategory.RowId}, ClassJobUse: {itemRow.ClassJobUse.RowId}");
#endif
            // ItemUICategory 61 = Miscellaneous, ClassJobCategory 38 = GLA/PLD, ClassJobUse 0 = None
            if (itemRow.ItemUICategory.RowId == 61 && itemRow.ClassJobCategory.RowId == 38 && itemRow.ClassJobUse.RowId == 0)
            {
#if DEBUG
                Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - Paladin Arms detected");
#endif
                if (ItemIsInInventory(itemInfo.ItemId))
                {
#if DEBUG
                    Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - Paladin Arms in inventory");
#endif
                    LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.InInventory, Position = position, Size = size });
                    continue;
                }

                var ItemArchives = DataManager.Excel.GetSubrowSheet<ArchiveItem>();

                var swordLookup = ItemArchives.TryGetSubrow(itemRow.AdditionalData.RowId, 0, out var sword);
                var shieldLookup = ItemArchives.TryGetSubrow(itemRow.AdditionalData.RowId, 1, out var shield);

                if (!swordLookup || !shieldLookup)
                {
#if DEBUG
                    if (!swordLookup)
                        Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - Failed to find sword with id {itemRow.AdditionalData.RowId}");
                    if (!shieldLookup)
                        Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - Failed to find shield with id {itemRow.AdditionalData.RowId}");
#endif
                    LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.NotObtained, Position = position, Size = size });
                    continue;
                }

                var swordId = sword.Item.RowId;
                var shieldId = shield.Item.RowId;

                if (!CabinetLookup.Value.TryGetValue(swordId, out var swordCabinetRowId))
                {
#if DEBUG
                    Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - failed to find sword in cabinet");
#endif
                    continue;
                }

                if (!CabinetLookup.Value.TryGetValue(shieldId, out var shieldCabinetRowId))
                {
#if DEBUG
                    Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - failed to find shield in cabinet");
#endif
                    continue;
                }

                var swordInCabinet = ItemIsInCabinet(swordCabinetRowId);
                var shieldInCabinet = ItemIsInCabinet(shieldCabinetRowId);

                var resultEntry = new LootEntry
                {
                    Position = position,
                    Size = size,
                    Status = swordInCabinet ? ItemStatus.Obtained : ItemStatus.NotObtained,
                    ShieldStatus = shieldInCabinet ? ItemStatus.Obtained : ItemStatus.NotObtained
                };

                if (swordInCabinet && shieldInCabinet)
                {
#if DEBUG
                    Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - sword and shield in cabinet");
#endif
                    LootOverlay.currentLoot.Add(resultEntry);
                    continue;
                }

                if (ItemIsInInventory(swordId))
                {
#if DEBUG
                    Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - sword in inventory");
#endif
                    resultEntry.Status = ItemStatus.InInventory;
                    continue;
                }

                if (ItemIsInInventory(shieldId))
                {
#if DEBUG
                    Log.Information($"Item: {itemRow.Name} ({itemRow.RowId}) - shield in inventory");
#endif
                    resultEntry.ShieldStatus = ItemStatus.InInventory;
                }

                LootOverlay.currentLoot.Add(resultEntry);
                continue;
            }


            if (!CabinetLookup.Value.TryGetValue(adjustedItemId, out var cabinetRowId))
            {
                continue;
            }

            if (ItemIsInCabinet(cabinetRowId))
            {
                LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.Obtained, Position = position, Size = size });
                continue;
            }

            if (ItemIsInInventory(itemInfo.ItemId))
            {
                LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.InInventory, Position = position, Size = size });
                continue;
            }

            LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.NotObtained, Position = position, Size = size });
        }
    }

    private bool ItemIsInCabinet(uint cabinetRowId)
    {
        (var byteIndex, var bitOffset) = Math.DivRem(cabinetRowId, 8);
        if (UIState.Instance()->Cabinet.UnlockedItems.Count >= byteIndex && (UIState.Instance()->Cabinet.UnlockedItems[(int)byteIndex] & (1 << (int)bitOffset)) != 0)
        {
            return true;
        }

        var itemFinderModule = ItemFinderModule.Instance();
        (byteIndex, bitOffset) = Math.DivRem(cabinetRowId - 1048, 32);
        if (itemFinderModule->CabinetItemUnlockBits.Length >= byteIndex)
        {
            if ((itemFinderModule->CabinetItemUnlockBits[(int)byteIndex] & (1 << (int)bitOffset)) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool ItemIsInInventory(uint ItemId)
    {
        var inventoryManager = InventoryManager.Instance();

        if (inventoryManager is null)
        {
            return false;
        }

        foreach (var bagType in bags)
        {
            var bag = inventoryManager->GetInventoryContainer(bagType);
            if (bag is null)
            {
                continue;
            }

            foreach (var i in Enumerable.Range(0, bag->Size))
            {
                var item = bag->Items[i];
                if (item.ItemId == 0)
                    continue;

                if (item.ItemId == ItemId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void ToggleConfigUi() => ConfigWindow.IsOpen = true;
}
