using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using ZDs.Helpers;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace ZDs.Timeline;

public partial class TimelineManager
{
    private const uint HighQualityItemOffset = 1_000_000;
    private readonly ExcelSheet<LuminaItem> _itemSheet = IDataManager.Get().GetExcelSheet<LuminaItem>();

    private void QueueUsedItem(uint nativeItemId)
    {
        uint itemId = NormalizeItemId(nativeItemId);
        if (!Config.SourcesConfig.ShouldTrackItem(itemId) || !_itemSheet.TryGetRow(itemId, out _))
        {
            return;
        }

        TimelineKey key = TimelineKey.ForItem(itemId);
        double now = ImGui.GetTime();
        _pendingSourceUses[key] = new PendingSourceUse(key, nativeItemId, now + SourceUseRetrySeconds);
        if (TryAddUsedItem(nativeItemId, now))
        {
            _pendingSourceUses.Remove(key);
        }
    }

    private bool TryAddUsedItem(uint nativeItemId, double now)
    {
        uint itemId = NormalizeItemId(nativeItemId);
        if (!_itemSheet.TryGetRow(itemId, out LuminaItem itemRow))
        {
            return false;
        }

        ActionsHelper actionHelper = new();
        ActionsHelper.NativeRecastInfo recast = actionHelper.GetNativeRecastInfo(ActionType.Item, nativeItemId);
        if (recast.RecastTime <= 0 && nativeItemId != itemId)
        {
            recast = actionHelper.GetNativeRecastInfo(ActionType.Item, itemId);
        }

        if (recast.RecastTime <= 0 || recast.Remaining <= 0.05f)
        {
            return false;
        }

        TimelineKey key = TimelineKey.ForItem(itemId);
        TimelineItem? existing = _items.FirstOrDefault(item => item.Key == key);
        if (existing is null)
        {
            EnsureItemCapacity();
            existing = new TimelineItem(
                key,
                itemRow.Icon,
                TimelineItemType.OffGCD,
                now - recast.RecastTimeElapsed,
                0,
                0,
                recast.RecastTime * 10,
                1)
            {
                ActiveCharges = 0,
            };
            _items.Add(existing);
        }
        else
        {
            existing.Time = now - recast.RecastTimeElapsed;
            existing.Cooldown = recast.RecastTime * 10;
            existing.ActiveCharges = 0;
        }

        SortTimeline();
        return true;
    }

    private static uint NormalizeItemId(uint itemId) =>
        itemId >= HighQualityItemOffset ? itemId - HighQualityItemOffset : itemId;
}
