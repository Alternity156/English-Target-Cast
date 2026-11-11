using Dalamud.Configuration;
using System;

namespace EnglishTargetCast;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Show English names on your target's cast bar.</summary>
    public bool TranslateTarget { get; set; } = true;

    /// <summary>Show English names on your focus target's cast bar.</summary>
    public bool TranslateFocusTarget { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
