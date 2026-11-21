using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace WillThisBeAGoodAdditionToMyCollection.Windows;

public struct LootEntry
{
    public Vector2 Position;
    public Vector2 Size;
    public required ItemStatus Status;
    public ItemStatus? ShieldStatus;
}

public enum ItemStatus
{
    NotArmoire,
    NotObtained,
    Obtained,
    InInventory,
}

public class LootOverlay : Window, IDisposable
{
    private readonly Plugin plugin;

    public List<LootEntry> currentLoot = new List<LootEntry>();

    public LootOverlay(Plugin plugin) : base("###LootOverlay")
    {
        this.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize |
                     ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        foreach (var item in currentLoot)
        {
            uint color = 0;
            switch (item.Status)
            {
                case ItemStatus.InInventory:
                    color = ImGui.GetColorU32(plugin.Configuration.ItemInInventoryRGBA);
                    break;
                case ItemStatus.NotObtained:
                    color = ImGui.GetColorU32(plugin.Configuration.ItemNotObtainedRGBA);
                    break;
                case ItemStatus.Obtained:
                    color = ImGui.GetColorU32(plugin.Configuration.ItemAlreadyObtainedRGBA);
                    break;
            }
            var padding = item.Size * new Vector2(0.20f, -0.20f);
            var position = ImGui.GetWindowPos() + item.Position + padding;
            ImGui.GetWindowDrawList().AddCircleFilled(position, 7f, color);

            if (item.ShieldStatus.HasValue)
            {
                uint shieldColor = 0;
                switch (item.ShieldStatus.Value)
                {
                    case ItemStatus.InInventory:
                        shieldColor = ImGui.GetColorU32(plugin.Configuration.ItemInInventoryRGBA);
                        break;
                    case ItemStatus.NotObtained:
                        shieldColor = ImGui.GetColorU32(plugin.Configuration.ItemNotObtainedRGBA);
                        break;
                    case ItemStatus.Obtained:
                        shieldColor = ImGui.GetColorU32(plugin.Configuration.ItemAlreadyObtainedRGBA);
                        break;
                }
                var shieldPosition = position + new Vector2(15f, 0f);
                ImGui.GetWindowDrawList().AddCircleFilled(shieldPosition, 7f, shieldColor);
            }
        }
    }
}
