using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Windowing;

namespace EnglishTargetCast.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("English Target Cast###EnglishTargetCastConfig")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (Plugin.DataManager.Language == ClientLanguage.English)
        {
            ImGui.TextWrapped("Your game client is already in English, so this plugin has nothing to change.");
            ImGui.Separator();
        }

        var target = configuration.TranslateTarget;
        if (ImGui.Checkbox("Target cast bar in English", ref target))
        {
            configuration.TranslateTarget = target;
            configuration.Save();
        }

        var focus = configuration.TranslateFocusTarget;
        if (ImGui.Checkbox("Focus target cast bar in English", ref focus))
        {
            configuration.TranslateFocusTarget = focus;
            configuration.Save();
        }

        ImGui.TextDisabled("Turning an option off takes effect on the next cast.");
    }
}
