using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Text.ReadOnly;
// "Action" also exists in the System namespace, so the game data sheets get aliases.
using ActionSheet = Lumina.Excel.Sheets.Action;
using EventActionSheet = Lumina.Excel.Sheets.EventAction;
using EventItemSheet = Lumina.Excel.Sheets.EventItem;
using ItemSheet = Lumina.Excel.Sheets.Item;
using MountSheet = Lumina.Excel.Sheets.Mount;

namespace EnglishTargetCast;

/// <summary>
/// Swaps the action name shown on the target / focus target cast bars for its English name.
/// </summary>
public sealed unsafe class CastBarTranslator : IDisposable
{
    // The game's UI windows ("addons") that show a cast bar.
    private const string SplitCastBarAddon = "_TargetInfoCastBar"; // target cast bar, when split from the target bar in HUD layout
    private const string TargetInfoAddon = "_TargetInfo";          // target bar with the cast bar built in
    private const string FocusTargetAddon = "_FocusTargetInfo";    // focus target bar

    // The ID of the text node holding the action name inside each addon.
    // Same IDs the SimpleTweaks plugin uses. Check them with /xldev > Addon Inspector after game patches.
    private const uint SplitCastBarTextNodeId = 4;
    private const uint TargetInfoTextNodeId = 12;
    private const uint FocusTargetTextNodeId = 5;

    private static readonly string[] Addons = [SplitCastBarAddon, TargetInfoAddon, FocusTargetAddon];

    private readonly Configuration config;
    private readonly ClientLanguage clientLanguage;

    // Looking up names is cheap, but this runs every frame, so remember results.
    // A null value means "no usable name for this cast", which is also worth remembering.
    private readonly Dictionary<(ActionType Type, uint Id), CastName?> nameCache = [];

    // The last translation made on each addon. The game can reset the cast bar text in the middle
    // of a cast (most noticeable on long casts), so this is used to put the English name straight back.
    private readonly Dictionary<string, Translation> lastTranslation = [];

    private bool errorLogged;
    private string? lastMismatch;

    private sealed record CastName(string Client, string English, byte[] EnglishUtf8);

    /// <param name="OriginalText">The exact text the game had put on the cast bar before it was replaced.</param>
    private sealed record Translation(byte[] OriginalText, CastName Name);

    public CastBarTranslator(Configuration config)
    {
        this.config = config;
        clientLanguage = Plugin.DataManager.Language;

        // PreDraw runs right before the game draws the addon, every frame it is visible.
        // Changing the text here means the game can't overwrite it before it reaches the screen.
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, Addons, OnPreDraw);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnPreDraw);
    }

    private void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        // An exception here would be logged by Dalamud every single frame, so catch it and log it once.
        try
        {
            Update(args);
        }
        catch (Exception ex)
        {
            if (!errorLogged)
            {
                Plugin.Log.Error(ex, "Failed to update a cast bar. Further errors will not be logged.");
                errorLogged = true;
            }
        }
    }

    private void Update(AddonArgs args)
    {
        var addon = args.GetAddonPointer<AtkUnitBase>();
        if (addon == null || !addon->IsVisible)
            return;

        switch (args.AddonName)
        {
            case SplitCastBarAddon when config.TranslateTarget:
                // The target bar shows your hard target, or your soft target if you have no hard target.
                Translate(addon, SplitCastBarAddon, SplitCastBarTextNodeId, Plugin.TargetManager.Target, Plugin.TargetManager.SoftTarget);
                break;

            case TargetInfoAddon when config.TranslateTarget:
                Translate(addon, TargetInfoAddon, TargetInfoTextNodeId, Plugin.TargetManager.Target, Plugin.TargetManager.SoftTarget);
                break;

            case FocusTargetAddon when config.TranslateFocusTarget:
                Translate(addon, FocusTargetAddon, FocusTargetTextNodeId, Plugin.TargetManager.FocusTarget, null);
                break;
        }
    }

    private void Translate(AtkUnitBase* addon, string addonName, uint textNodeId, IGameObject? first, IGameObject? second)
    {
        var textNode = addon->GetTextNodeById(textNodeId);
        if (textNode == null || !textNode->IsVisible())
            return;

        var shownBytes = textNode->NodeText.AsSpan();

        if (lastTranslation.TryGetValue(addonName, out var last))
        {
            // Still showing our English name: nothing to do. Fast byte check, no allocation.
            if (shownBytes.SequenceEqual(last.Name.EnglishUtf8))
                return;

            // The game put back the exact text we replaced. Restore the English name without
            // checking the target again, since its cast state isn't reliable this late in a cast.
            if (shownBytes.SequenceEqual(last.OriginalText))
            {
                textNode->SetText(last.Name.English);
                return;
            }
        }

        // New text on the cast bar: try the hard target first, then the soft target.
        var translation = TryTranslate(textNode, first) ?? TryTranslate(textNode, second);
        if (translation != null)
            lastTranslation[addonName] = translation;
    }

    /// <returns>The translation that was applied, or null if the text was left alone.</returns>
    private Translation? TryTranslate(AtkTextNode* textNode, IGameObject? obj)
    {
        if (obj is not IBattleChara caster || !caster.IsCasting)
            return null;

        var name = GetCastName((ActionType)caster.CastActionType, caster.CastActionId);
        if (name == null)
            return null;

        var shownBytes = textNode->NodeText.AsSpan();

        // Only replace the text if it really is this cast's name in the client language.
        // This guards against replacing something else, like the "Interrupted" text,
        // or a cast from a different target than the one we looked at.
        var shown = Clean(new ReadOnlySeStringSpan(shownBytes).ExtractText());
        if (!shown.Equals(name.Client, StringComparison.OrdinalIgnoreCase))
        {
            // Helps debugging if the names never match (open /xllog and set the level to Debug).
            if (shown.Length > 0 && shown != lastMismatch)
            {
                lastMismatch = shown;
                Plugin.Log.Debug($"Cast bar shows \"{shown}\", expected \"{name.Client}\". Leaving it alone.");
            }

            return null;
        }

        // Copy the original text first: SetText overwrites the buffer shownBytes points into.
        var translation = new Translation(shownBytes.ToArray(), name);
        textNode->SetText(name.English);
        return translation;
    }

    private CastName? GetCastName(ActionType type, uint id)
    {
        if (nameCache.TryGetValue((type, id), out var cached))
            return cached;

        var name = type switch
        {
            ActionType.Action => Lookup<ActionSheet>(id, row => row.Name),

            // Item IDs: + 1,000,000 for high quality, + 500,000 for collectables, 2,000,000+ for key items.
            ActionType.Item when id >= 2_000_000 => Lookup<EventItemSheet>(id, row => row.Name),
            ActionType.Item => Lookup<ItemSheet>(id % 500_000, row => row.Name),
            ActionType.EventItem => Lookup<EventItemSheet>(id, row => row.Name),

            // Interacting with objects: "Opening", "Searching", etc.
            ActionType.EventAction => Lookup<EventActionSheet>(id, row => row.Name),

            // Mount names are stored in lowercase ("company chocobo") so they get capitalized.
            ActionType.Mount => Lookup<MountSheet>(id, row => row.Singular, titleCase: true),

            _ => null,
        };

        nameCache[(type, id)] = name;
        return name;
    }

    private CastName? Lookup<T>(uint id, Func<T, ReadOnlySeString> getName, bool titleCase = false)
        where T : struct, IExcelRow<T>
    {
        // Every global client includes the game text in English, Japanese, German and French,
        // so the English name is read straight from the game files. No internet or translation needed.
        if (!Plugin.DataManager.GetExcelSheet<T>(clientLanguage).TryGetRow(id, out var clientRow) ||
            !Plugin.DataManager.GetExcelSheet<T>(ClientLanguage.English).TryGetRow(id, out var englishRow))
            return null;

        var client = Clean(getName(clientRow).ExtractText());
        var english = Clean(getName(englishRow).ExtractText());
        if (client.Length == 0 || english.Length == 0)
            return null;

        if (titleCase)
            english = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(english);

        return new CastName(client, english, Encoding.UTF8.GetBytes(english));
    }

    // Removes soft hyphens (invisible line-break hints common in German and French text) and stray spaces.
    private static string Clean(string text) => text.Replace("­", string.Empty).Trim();
}
