using Dalamud.Configuration;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;

namespace WillThisBeAGoodAdditionToMyCollection;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public Vector4 ItemAlreadyObtainedRGBA { get; set; } = new Vector4(0f, 1f, 0f, 1f);
    public Vector4 ItemInInventoryRGBA { get; set; } = new Vector4(0f, 1f, 0f, 1f);
    public Vector4 ItemNotObtainedRGBA { get; set; } = new Vector4(1f, 0.5f, 0f, 1f);

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
