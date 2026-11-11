using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EnglishTargetCast.Windows;

namespace EnglishTargetCast;

public sealed class Plugin : IDalamudPlugin
{
    // Dalamud fills these in ("injects" them) before the constructor runs.
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/englishcast";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("EnglishTargetCast");
    private ConfigWindow ConfigWindow { get; init; }

    // Null when the client is already in English, since there's nothing to translate.
    private readonly CastBarTranslator? castBarTranslator;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the English Target Cast settings."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        if (DataManager.Language == ClientLanguage.English)
        {
            Log.Information("Client language is English, cast bars are left as they are.");
        }
        else
        {
            castBarTranslator = new CastBarTranslator(Configuration);
            Log.Information($"Showing cast bars in English (client language: {DataManager.Language}).");
        }
    }

    public void Dispose()
    {
        // Undo everything the constructor did, so the plugin can be reloaded cleanly.
        castBarTranslator?.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => ToggleConfigUi();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
