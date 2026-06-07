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

    //private IReadOnlySet<Lumina.Excel.Sheets.Cabinet> ArmoireItems => field != null ? field : field = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>().Where(r => r.Item.RowId != 0).ToHashSet();
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
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager is null)
            return;

        var addon = (AddonNeedGreed*)args.Addon.Address;
        var widgetScale = addon->Scale;

        var listComponentNode = (AtkComponentNode*)addon->AtkUnitBase.GetNodeById(6);
        if (listComponentNode is null)
            return;

        var atkListComponent = (AtkComponentList*)listComponentNode->Component;
        if (atkListComponent is null)
            return;

        LootOverlay.currentLoot.Clear();

        foreach (var index in Enumerable.Range(0, addon->Items.Length))
        {
            ref var itemInfo = ref addon->Items[index];

            if (itemInfo.ItemId is 0)
                continue;

            var listItemRenderer = atkListComponent->GetItemRenderer(index);
            var uiNode = listItemRenderer->AtkResNode;
            var size = new Vector2(uiNode->Width, uiNode->Height) * widgetScale;
            var position = new Vector2(uiNode->ScreenX - addon->X, uiNode->ScreenY - addon->Y + size.Y);

            var adjustedItemId = itemInfo.ItemId > 1_000_000 ? itemInfo.ItemId - 1_000_000 : itemInfo.ItemId;
            if (!CabinetLookup.Value.TryGetValue(adjustedItemId, out var cabinetRowId))
            {
                //LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.NotArmoire, Position = position, Size = size });
                continue;
            }

            (var byteIndex, var bitOffset) = Math.DivRem(cabinetRowId, 8);
            if (UIState.Instance()->Cabinet.UnlockedItems.Count >= byteIndex && (UIState.Instance()->Cabinet.UnlockedItems[(int)byteIndex] & (1 << (int)bitOffset)) != 0)
            {
                LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.Obtained, Position = position, Size = size });
                continue;
            }

            var itemFinderModule = ItemFinderModule.Instance();
            (byteIndex, bitOffset) = Math.DivRem(cabinetRowId - 1048, 32);
            if (itemFinderModule->CabinetItemUnlockBits.Length >= byteIndex)
            {
                if ((itemFinderModule->CabinetItemUnlockBits[(int)byteIndex] & (1 << (int)bitOffset)) != 0)
                {
                    LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.Obtained, Position = position, Size = size });
                    continue;
                }
            }

            List<InventoryType> bags = new List<InventoryType>()
            {
                InventoryType.EquippedItems, InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2,
                InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
                InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets,
                InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand,
                InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
            };

            var foundInInventory = false;
            foreach (var bagType in bags)
            {
                var exitLoop = false;
                var bag = inventoryManager->GetInventoryContainer(bagType);
                if (bag is null)
                    continue;

                for (int i = 0; i < bag->Size; i++)
                {
                    var item = bag->Items[i];
                    if (item.ItemId == 0)
                        continue;

                    if (item.ItemId == itemInfo.ItemId)
                    {
                        foundInInventory = true;
                        exitLoop = true;
                        break;
                    }
                }

                if (exitLoop)
                    break;
            }

            if (foundInInventory)
            {
                LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.InInventory, Position = position, Size = size });
                continue;
            }

            LootOverlay.currentLoot.Add(new LootEntry { Status = ItemStatus.NotObtained, Position = position, Size = size });
        }
    }

    public void ToggleConfigUi() => ConfigWindow.IsOpen = true;
}
