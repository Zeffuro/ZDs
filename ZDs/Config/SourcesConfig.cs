using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using ZDs.Helpers;

namespace ZDs.Config;

public enum TargetLetterCorner
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
}

public sealed class SourcesConfig : IConfigPage
{
    [JsonIgnore]
    public bool Active { get; set; }

    public string Name => "Tracking";

    public bool TrackItems;
    public bool TrackDutyActions;
    public bool TrackLostActions;
    public bool TrackPhantomActions;
    public bool TrackDotsAndDebuffs;
    public bool TrackBuffs;
    public bool ShowDotTargetLetter = true;
    public TargetLetterCorner DotTargetLetterCorner = TargetLetterCorner.BottomLeft;
    public bool GroupBrdDots = true;
    public int MaxDotsAndDebuffs = 20;

    public bool FilterItems;
    public int ItemFilterMode;
    public SortedList<string, uint> FilteredItems = [];

    public bool FilterStatuses;
    public int StatusFilterMode;
    public SortedList<string, uint> FilteredStatuses = [];

    [JsonIgnore]
    private string _itemInput = string.Empty;

    [JsonIgnore]
    private string _statusInput = string.Empty;

    public IConfigPage GetDefault() => new SourcesConfig();

    public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
    {
        using var child = ImRaii.Child("##TimelineSources", size, border);
        if (!child)
        {
            return;
        }

        ImGui.TextWrapped("Choose what appears on the timeline.");
        ImGui.NewLine();

        ImGui.Checkbox("Items", ref TrackItems);
        DrawHelper.SetTooltip("Track item cooldowns.");
        if (TrackItems)
        {
            DrawItemFilter();
        }

        ImGui.Checkbox("Duty Actions", ref TrackDutyActions);
        DrawHelper.SetTooltip("Track regular duty actions.");

        ImGui.Checkbox("Lost Actions", ref TrackLostActions);
        DrawHelper.SetTooltip("Track equipped Lost Actions.");

        ImGui.Checkbox("Phantom Actions", ref TrackPhantomActions);
        DrawHelper.SetTooltip("Track equipped Phantom Actions.");

        ImGui.Checkbox("DoTs and Debuffs", ref TrackDotsAndDebuffs);
        DrawHelper.SetTooltip("Track harmful effects you apply to enemies.");
        if (TrackDotsAndDebuffs)
        {
            DrawHelper.DrawNestIndicator(1);
            ImGui.Checkbox("Target Letter", ref ShowDotTargetLetter);
            DrawHelper.SetTooltip("Show the enemy-list letter on each effect.");
            if (ShowDotTargetLetter)
            {
                DrawHelper.DrawNestIndicator(2);
                int targetLetterCorner = (int)DotTargetLetterCorner;
                if (ImGui.Combo("Corner##TargetLetter", ref targetLetterCorner, ["Top Left", "Top Right", "Bottom Left", "Bottom Right"], 4))
                {
                    DotTargetLetterCorner = (TargetLetterCorner)targetLetterCorner;
                }
            }
            DrawHelper.DrawNestIndicator(1);
            ImGui.Checkbox("Combine BRD DoTs", ref GroupBrdDots);
            DrawHelper.SetTooltip("Show BRD DoTs as Iron Jaws.");
        }

        ImGui.Checkbox("Buffs", ref TrackBuffs);
        DrawHelper.SetTooltip("Track temporary effects on you.");

        if (TrackDotsAndDebuffs || TrackBuffs)
        {
            DrawHelper.DrawNestIndicator(1);
            ImGui.DragInt("Max Effects", ref MaxDotsAndDebuffs, 1, 1, 50);
            MaxDotsAndDebuffs = Math.Clamp(MaxDotsAndDebuffs, 1, 50);
            DrawStatusFilter();
        }
    }

    public bool ShouldTrackItem(uint itemId)
    {
        if (!FilterItems)
        {
            return true;
        }

        bool listed = FilteredItems.ContainsValue(itemId);
        return ItemFilterMode == 0 ? !listed : listed;
    }

    public bool ShouldTrackStatus(uint statusId)
    {
        if (!FilterStatuses)
        {
            return true;
        }

        bool listed = FilteredStatuses.ContainsValue(statusId);
        return StatusFilterMode == 0 ? !listed : listed;
    }

    private void DrawItemFilter()
    {
        DrawHelper.DrawNestIndicator(1);
        ImGui.Checkbox("Filter Items", ref FilterItems);
        DrawHelper.SetTooltip("Limit which items appear.");
        if (!FilterItems)
        {
            return;
        }

        DrawHelper.DrawNestIndicator(2);
        ImGui.RadioButton("Hide listed", ref ItemFilterMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton("Only show listed", ref ItemFilterMode, 1);

        ExcelSheet<Item> sheet = IDataManager.Get().GetExcelSheet<Item>();
        DrawHelper.DrawNestIndicator(2);
        ImGui.SetNextItemWidth(290f);
        if (ImGui.InputTextWithHint("##ItemFilterInput", "Exact item name or ID", ref _itemInput, 80, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            AddItemFilterEntry(_itemInput, sheet);
            ImGui.SetKeyboardFocusHere(-1);
        }

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString()))
            {
                AddItemFilterEntry(_itemInput, sheet);
            }
        }

        ImGuiTableFlags flags = ImGuiTableFlags.RowBg |
                                ImGuiTableFlags.Borders |
                                ImGuiTableFlags.ScrollY |
                                ImGuiTableFlags.SizingStretchProp;
        int removeIndex = -1;
        DrawHelper.DrawNestIndicator(2);
        using var table = ImRaii.Table("##ItemFilterTable", 4, flags, new Vector2(Math.Max(280f, ImGui.GetContentRegionAvail().X - 18f), 170f));
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableHeadersRow();
        for (int index = 0; index < FilteredItems.Count; index++)
        {
            using var id = ImRaii.PushId(index);
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 30f);
            ImGui.TableSetColumnIndex(0);
            if (sheet.GetRowOrDefault(FilteredItems.Values[index]) is Item item)
            {
                DrawHelper.DrawIcon<Item>(item, ImGui.GetCursorPos(), new Vector2(28f), false, false);
            }
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(FilteredItems.Keys[index]);
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(FilteredItems.Values[index].ToString());
            ImGui.TableSetColumnIndex(3);
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
            FilteredItems.RemoveAt(removeIndex);
        }
    }

    private void AddItemFilterEntry(string input, ExcelSheet<Item> sheet)
    {
        string value = input.Trim();
        if (value.Length == 0)
        {
            return;
        }

        Item? found = null;
        if (uint.TryParse(value, out uint itemId))
        {
            found = sheet.GetRowOrDefault(itemId);
        }

        found ??= sheet.FirstOrDefault(item =>
            string.Equals(item.Name.ExtractText(), value, StringComparison.OrdinalIgnoreCase));
        if (found is not Item item || item.RowId == 0 || item.ItemAction.RowId == 0)
        {
            return;
        }

        string key = $"{item.Name.ExtractText()} [{item.RowId}]";
        if (!FilteredItems.ContainsKey(key))
        {
            FilteredItems.Add(key, item.RowId);
        }

        _itemInput = string.Empty;
    }

    private void DrawStatusFilter()
    {
        DrawHelper.DrawNestIndicator(1);
        ImGui.Checkbox("Filter Effects", ref FilterStatuses);
        DrawHelper.SetTooltip("Limit which effects appear.");
        if (!FilterStatuses)
        {
            return;
        }

        DrawHelper.DrawNestIndicator(2);
        ImGui.RadioButton("Hide listed##Statuses", ref StatusFilterMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton("Only show listed##Statuses", ref StatusFilterMode, 1);

        ExcelSheet<Status> sheet = IDataManager.Get().GetExcelSheet<Status>();
        DrawHelper.DrawNestIndicator(2);
        ImGui.SetNextItemWidth(290f);
        if (ImGui.InputTextWithHint("##StatusFilterInput", "Effect name or ID", ref _statusInput, 80, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            AddStatusFilterEntry(_statusInput, sheet);
            ImGui.SetKeyboardFocusHere(-1);
        }

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString() + "##Status"))
            {
                AddStatusFilterEntry(_statusInput, sheet);
            }
        }

        ImGuiTableFlags flags = ImGuiTableFlags.RowBg |
                                ImGuiTableFlags.Borders |
                                ImGuiTableFlags.ScrollY |
                                ImGuiTableFlags.SizingStretchProp;
        int removeIndex = -1;
        DrawHelper.DrawNestIndicator(2);
        using var table = ImRaii.Table("##StatusFilterTable", 4, flags, new Vector2(Math.Max(280f, ImGui.GetContentRegionAvail().X - 18f), 170f));
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableSetupColumn("Effect", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableHeadersRow();
        for (int index = 0; index < FilteredStatuses.Count; index++)
        {
            using var id = ImRaii.PushId(index);
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 30f);
            ImGui.TableSetColumnIndex(0);
            if (sheet.GetRowOrDefault(FilteredStatuses.Values[index]) is Status status)
            {
                DrawHelper.DrawIcon<Status>(status, ImGui.GetCursorPos(), new Vector2(28f), false, true);
            }
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(FilteredStatuses.Keys[index]);
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(FilteredStatuses.Values[index].ToString());
            ImGui.TableSetColumnIndex(3);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString() + "##Status"))
                {
                    removeIndex = index;
                }
            }
        }

        if (removeIndex >= 0)
        {
            FilteredStatuses.RemoveAt(removeIndex);
        }
    }

    private void AddStatusFilterEntry(string input, ExcelSheet<Status> sheet)
    {
        string value = input.Trim();
        if (value.Length == 0)
        {
            return;
        }

        Status? found = null;
        if (uint.TryParse(value, out uint statusId))
        {
            found = sheet.GetRowOrDefault(statusId);
        }

        found ??= sheet.FirstOrDefault(status =>
            status.StatusCategory is 1 or 2 &&
            !status.IsPermanent &&
            string.Equals(status.Name.ExtractText(), value, StringComparison.OrdinalIgnoreCase));
        if (found is not Status status || status.RowId == 0 || status.StatusCategory is not (1 or 2) || status.IsPermanent)
        {
            return;
        }

        string key = $"{status.Name.ExtractText()} [{status.RowId}]";
        if (!FilteredStatuses.ContainsKey(key))
        {
            FilteredStatuses.Add(key, status.RowId);
        }

        _statusInput = string.Empty;
    }
}
