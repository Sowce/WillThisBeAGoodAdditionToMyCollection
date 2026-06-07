using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace WillThisBeAGoodAdditionToMyCollection.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Will This Be A Good Addition To My Collection")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(500, 300);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped("This plugin will display a small colored dot on items you can collect in your Armoire, you can personalize the color of these dots.");
        ImGui.Separator();

        var ItemNotObtainedRGBA = configuration.ItemNotObtainedRGBA;
        ItemNotObtainedRGBA = ColorPickerWithPalette(1, "When I don't have the item", ItemNotObtainedRGBA, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
        ImGui.SameLine();
        ImGui.Text("When I don't have the item");

        var ItemAlreadyObtainedRGBA = configuration.ItemAlreadyObtainedRGBA;
        ItemAlreadyObtainedRGBA = ColorPickerWithPalette(2, "When I already have the item", ItemAlreadyObtainedRGBA, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
        ImGui.SameLine();
        ImGui.Text("When I already have the item");

        var ItemInInventoryRGBA = configuration.ItemInInventoryRGBA;
        ItemInInventoryRGBA = ColorPickerWithPalette(3, "When the item is in my inventory", ItemInInventoryRGBA, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
        ImGui.SameLine();
        ImGui.Text("When the item is in my inventory");

        if (ItemNotObtainedRGBA != configuration.ItemNotObtainedRGBA || ItemAlreadyObtainedRGBA != configuration.ItemAlreadyObtainedRGBA || ItemInInventoryRGBA != configuration.ItemInInventoryRGBA)
        {
            configuration.ItemNotObtainedRGBA = ItemNotObtainedRGBA;
            configuration.ItemAlreadyObtainedRGBA = ItemAlreadyObtainedRGBA;
            configuration.ItemInInventoryRGBA = ItemInInventoryRGBA;
            configuration.Save();
        }
    }

    // using this one because ImGuiComponents.ColorPickerWithPalette does not support having flags on the button
    static Vector4 ColorPickerWithPalette(int id, string description, Vector4 originalColor, ImGuiColorEditFlags flags)
    {
        var existingColor = originalColor;
        var selectedColor = originalColor;
        var colorPalette = ImGuiHelpers.DefaultColorPalette(36);
        if (ImGui.ColorButton($"{description}###ColorPickerButton{id}", originalColor, flags))
        {
            ImGui.OpenPopup($"###ColorPickerPopup{id}");
        }

        using var popup = ImRaii.Popup($"###ColorPickerPopup{id}");

        if (popup)
        {
            if (ImGui.ColorPicker4($"###ColorPicker{id}", ref existingColor, flags))
            {
                selectedColor = existingColor;
            }

            for (var i = 0; i < 4; i++)
            {
                ImGui.Spacing();
                for (var j = i * 9; j < (i * 9) + 9; j++)
                {
                    if (ImGui.ColorButton($"###ColorPickerSwatch{id}{i}{j}", colorPalette[j]))
                    {
                        selectedColor = colorPalette[j];
                    }

                    ImGui.SameLine();
                }
            }
        }

        return selectedColor;
    }
}
