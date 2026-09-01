using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel;
using Newtonsoft.Json;
using ZDs.Helpers;
using Action = Lumina.Excel.Sheets.Action;

namespace ZDs.Config;

public enum TimelinePriority
{
    Normal = 0,
    Important = 1,
    Critical = 2,
}

public enum AbilityAlertMoment
{
    Ready = 0,
    Threshold = 1,
}

public enum TimelinePriorityEffect
{
    Outline = 0,
    Breathe = 1,
    Ants = 2,
}

public sealed class AbilityPriorityRule
{
    public uint ActionId;
    public string Name = string.Empty;
    public TimelinePriority Priority = TimelinePriority.Important;
    public AbilityAlertMoment AlertMoment;
    public int SoundId;
}

public sealed class AbilityPriorityConfig
{
    public List<AbilityPriorityRule> Rules = [];
    public ConfigColor ImportantColor = new(1f, 0.84f, 0f, 1f);
    public ConfigColor CriticalColor = new(1f, 0.27f, 0f, 1f);
    public TimelinePriorityEffect ImportantEffect = TimelinePriorityEffect.Outline;
    public TimelinePriorityEffect CriticalEffect = TimelinePriorityEffect.Breathe;
    public bool UsePriorityStyleForThreshold;
    public bool UsePriorityColorForFinish;

    [JsonIgnore]
    private string _input = string.Empty;

    private static readonly string[] SoundNames = ["Off", .. Enumerable.Range(1, 16).Select(id => $"Sound {id}")];

    public AbilityPriorityRule? Find(uint actionId) => Rules.FirstOrDefault(rule => rule.ActionId == actionId);

    public TimelinePriority GetPriority(uint actionId) => Find(actionId)?.Priority ?? TimelinePriority.Normal;

    public Vector4 GetColor(TimelinePriority priority) => priority == TimelinePriority.Critical
        ? CriticalColor.Vector
        : ImportantColor.Vector;

    public int GetLayoutHash()
    {
        HashCode hash = new();
        foreach (AbilityPriorityRule rule in Rules.OrderBy(rule => rule.ActionId))
        {
            hash.Add(rule.ActionId);
            hash.Add(rule.Priority);
        }
        return hash.ToHashCode();
    }

    public void Draw(ExcelSheet<Action> sheet)
    {
        ImGui.NewLine();
        ImGui.Separator();
        ImGui.TextUnformatted("Priority Abilities");
        ImGui.TextDisabled("Keep important abilities visible and optionally play a sound.");

        DrawHelper.DrawColorSelector("Important Color", ref ImportantColor);
        int importantEffect = (int)ImportantEffect;
        if (ImGui.Combo("Important Effect", ref importantEffect, ["Outline", "Breathe", "Ants"], 3))
        {
            ImportantEffect = (TimelinePriorityEffect)importantEffect;
        }

        DrawHelper.DrawColorSelector("Critical Color", ref CriticalColor);
        int criticalEffect = (int)CriticalEffect;
        if (ImGui.Combo("Critical Effect", ref criticalEffect, ["Outline", "Breathe", "Ants"], 3))
        {
            CriticalEffect = (TimelinePriorityEffect)criticalEffect;
        }

        ImGui.Checkbox("Use Priority Style for Threshold", ref UsePriorityStyleForThreshold);
        DrawHelper.SetTooltip("Use the priority color and effect instead of threshold Ants for priority abilities.");
        ImGui.Checkbox("Use Priority Color at Zero", ref UsePriorityColorForFinish);
        DrawHelper.SetTooltip("Carry the ability's priority color into its finish animation.");
        ImGui.NewLine();

        ImGui.SetNextItemWidth(300f);
        if (ImGui.InputTextWithHint("##PriorityAbility", "Exact ability name or ID", ref _input, 80, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            Add(_input, sheet);
            ImGui.SetKeyboardFocusHere(-1);
        }

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString() + "##Priority"))
            {
                Add(_input, sheet);
            }
        }

        ImGuiTableFlags flags = ImGuiTableFlags.RowBg |
                                ImGuiTableFlags.Borders |
                                ImGuiTableFlags.ScrollY |
                                ImGuiTableFlags.SizingStretchProp;
        int removeIndex = -1;
        using var table = ImRaii.Table("##PriorityAbilities", 6, flags, new Vector2(0f, 190f));
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableSetupColumn("Ability", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.WidthFixed, 88f);
        ImGui.TableSetupColumn("Sound At", ImGuiTableColumnFlags.WidthFixed, 82f);
        ImGui.TableSetupColumn("Sound", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableHeadersRow();

        for (int index = 0; index < Rules.Count; index++)
        {
            AbilityPriorityRule rule = Rules[index];
            using var id = ImRaii.PushId(index);
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 32f);
            ImGui.TableSetColumnIndex(0);
            if (sheet.GetRowOrDefault(rule.ActionId) is Action action)
            {
                DrawHelper.DrawIcon<Action>(action, ImGui.GetCursorPos(), new Vector2(28f), false, false);
            }

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(rule.Name);

            ImGui.TableSetColumnIndex(2);
            int priority = (int)rule.Priority - 1;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo("##Priority", ref priority, ["Important", "Critical"], 2))
            {
                rule.Priority = (TimelinePriority)(priority + 1);
            }

            ImGui.TableSetColumnIndex(3);
            int moment = (int)rule.AlertMoment;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo("##Moment", ref moment, ["Ready", "Threshold"], 2))
            {
                rule.AlertMoment = (AbilityAlertMoment)moment;
            }

            ImGui.TableSetColumnIndex(4);
            int sound = Math.Clamp(rule.SoundId, 0, 16);
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo("##Sound", ref sound, SoundNames, SoundNames.Length))
            {
                rule.SoundId = sound;
                if (sound > 0)
                {
                    UIGlobals.PlayChatSoundEffect((uint)sound);
                }
            }

            ImGui.TableSetColumnIndex(5);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString()))
                {
                    removeIndex = index;
                }
            }
        }

        if (removeIndex >= 0)
        {
            Rules.RemoveAt(removeIndex);
        }
    }

    private void Add(string input, ExcelSheet<Action> sheet)
    {
        string value = input.Trim();
        if (value.Length == 0)
        {
            return;
        }

        Action? found = null;
        if (uint.TryParse(value, out uint actionId))
        {
            Action? candidate = sheet.GetRowOrDefault(actionId);
            if (candidate is Action candidateAction &&
                candidateAction.RowId != 0 &&
                (candidateAction.IsPlayerAction || candidateAction.RowId is 3 or 6 or 37018))
            {
                found = candidateAction;
            }
        }

        found ??= sheet.FirstOrDefault(action =>
            (action.IsPlayerAction || action.RowId is 3 or 6 or 37018) &&
            string.Equals(action.Name.ExtractText(), value, StringComparison.OrdinalIgnoreCase));
        if (found is not Action action || action.RowId == 0)
        {
            return;
        }

        if (Find(action.RowId) == null)
        {
            Rules.Add(new AbilityPriorityRule
            {
                ActionId = action.RowId,
                Name = action.Name.ExtractText(),
            });
        }

        _input = string.Empty;
    }
}
