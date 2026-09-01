using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ZDs.Helpers;
using LuminaStatus = Lumina.Excel.Sheets.Status;
using NativeStatus = FFXIVClientStructs.FFXIV.Client.Game.Status;

namespace ZDs.Timeline;

public partial class TimelineManager
{
    private const uint BrdCombinedDotKeyId = 0x80000000u | 3560u;
    private const uint BrdCombinedDotIconId = 2608;
    private static readonly HashSet<uint> _brdBiteStatusIds = [124, 1200, 1205, 1321];
    private static readonly HashSet<uint> _brdWindStatusIds = [129, 1201, 1206, 1322];

    private readonly ExcelSheet<LuminaStatus> _statusSheet = IDataManager.Get().GetExcelSheet<LuminaStatus>();
    private readonly HashSet<uint> _harmfulStatusIds = IDataManager.Get().GetExcelSheet<LuminaStatus>()
        .Where(status => !status.IsPermanent && status.StatusCategory == 2)
        .Select(status => status.RowId)
        .ToHashSet();
    private readonly HashSet<uint> _beneficialStatusIds = IDataManager.Get().GetExcelSheet<LuminaStatus>()
        .Where(status => !status.IsPermanent && status.StatusCategory == 1)
        .Select(status => status.RowId)
        .ToHashSet();
    private readonly Dictionary<TimelineKey, float> _statusDurations = [];

    private unsafe void SynchronizeStatuses(double now)
    {
        IPlayerCharacter? player = IObjectTable.Get().LocalPlayer;
        if (player is null)
        {
            RemoveSource(TimelineSourceKind.Status);
            return;
        }

        if (Config.SourcesConfig.TrackDotsAndDebuffs)
        {
            EnemyListHelper.Update();
        }
        else
        {
            EnemyListHelper.Clear();
        }

        CharacterManager* characterManager = CharacterManager.Instance();
        if (characterManager == null)
        {
            RemoveSource(TimelineSourceKind.Status);
            return;
        }

        List<StatusSnapshot> snapshots = [];
        HashSet<TimelineKey> observedStatuses = [];
        foreach (BattleChara* battleChara in characterManager->BattleCharas)
        {
            if (battleChara == null)
            {
                continue;
            }

            ulong targetObjectId = battleChara->GetGameObjectId();
            bool isPlayer = targetObjectId == player.GameObjectId;
            if (isPlayer ? !Config.SourcesConfig.TrackBuffs :
                !Config.SourcesConfig.TrackDotsAndDebuffs || !battleChara->GetIsTargetable())
            {
                continue;
            }

            ref StatusManager statusManager = ref battleChara->StatusManager;
            for (int index = 0; index < statusManager.NumValidStatuses; index++)
            {
                ref NativeStatus status = ref statusManager.Status[index];
                bool trackedStatus = isPlayer
                    ? _beneficialStatusIds.Contains(status.StatusId)
                    : _harmfulStatusIds.Contains(status.StatusId) && status.SourceObject.ObjectId == player.EntityId;
                if (!trackedStatus ||
                    status.RemainingTime <= 0 ||
                    !Config.SourcesConfig.ShouldTrackStatus(status.StatusId) ||
                    !_statusSheet.TryGetRow(status.StatusId, out LuminaStatus statusRow))
                {
                    continue;
                }

                float remaining = status.RemainingTime;
                TimelineKey key = TimelineKey.ForStatus(status.StatusId, status.SourceObject.Id, targetObjectId);
                observedStatuses.Add(key);
                if (!_statusDurations.TryGetValue(key, out float duration) || remaining > duration)
                {
                    duration = remaining;
                    _statusDurations[key] = duration;
                }

                uint iconId = statusRow.Icon;
                if (statusRow.MaxStacks > 0 && status.Param > 0)
                {
                    iconId += (uint)Math.Max(0, status.Param - 1);
                }

                char? marker = !isPlayer && Config.SourcesConfig.ShowDotTargetLetter
                    ? EnemyListHelper.GetMarker((uint)targetObjectId)
                    : null;
                snapshots.Add(new StatusSnapshot(
                    key,
                    iconId,
                    remaining,
                    duration,
                    marker?.ToString()));
            }
        }

        foreach (TimelineKey key in _statusDurations.Keys.Where(key => !observedStatuses.Contains(key)).ToList())
        {
            _statusDurations.Remove(key);
        }

        if (Config.SourcesConfig.GroupBrdDots)
        {
            snapshots = GroupBrdDots(snapshots);
        }

        HashSet<TimelineKey> seen = [];
        foreach (StatusSnapshot snapshot in snapshots
                     .OrderBy(snapshot => snapshot.Remaining)
                     .Take(Math.Clamp(Config.SourcesConfig.MaxDotsAndDebuffs, 1, 50)))
        {
            seen.Add(snapshot.Key);
            TimelineItem? item = _items.FirstOrDefault(item => item.Key == snapshot.Key);
            if (item is null)
            {
                EnsureItemCapacity();
                item = new TimelineItem(
                    snapshot.Key,
                    snapshot.IconId,
                    TimelineItemType.OffGCD,
                    now,
                    0,
                    0,
                    snapshot.Duration * 10,
                    1);
                _items.Add(item);
            }

            item.Cooldown = snapshot.Duration * 10;
            item.Time = now - Math.Max(0, snapshot.Duration - snapshot.Remaining);
            item.ActiveCharges = 0;
            item.BadgeText = snapshot.BadgeText;
        }

        foreach (TimelineItem item in _items
                     .Where(item => item.Key.SourceKind == TimelineSourceKind.Status && !seen.Contains(item.Key))
                     .ToList())
        {
            double remaining = item.Cooldown / 10d - (now - item.Time);
            if (remaining <= 0.5d)
            {
                CompleteAndRemove(item, now);
            }
            else
            {
                _items.Remove(item);
            }
        }

        SortTimeline();
    }

    private static List<StatusSnapshot> GroupBrdDots(List<StatusSnapshot> snapshots)
    {
        List<StatusSnapshot> grouped = [.. snapshots];

        foreach (IGrouping<(ulong Owner, ulong Target), StatusSnapshot> targetDots in snapshots
                     .Where(snapshot => _brdBiteStatusIds.Contains(snapshot.Key.Id) ||
                                        _brdWindStatusIds.Contains(snapshot.Key.Id))
                     .GroupBy(snapshot => (snapshot.Key.OwnerObjectId, snapshot.Key.TargetObjectId)))
        {
            List<StatusSnapshot> bites = targetDots
                .Where(snapshot => _brdBiteStatusIds.Contains(snapshot.Key.Id))
                .ToList();
            List<StatusSnapshot> winds = targetDots
                .Where(snapshot => _brdWindStatusIds.Contains(snapshot.Key.Id))
                .ToList();
            if (bites.Count == 0 || winds.Count == 0)
            {
                continue;
            }

            StatusSnapshot biteDot = bites.MinBy(snapshot => snapshot.Remaining);
            StatusSnapshot windDot = winds.MinBy(snapshot => snapshot.Remaining);
            if (Math.Abs(biteDot.Remaining - windDot.Remaining) >= 1f)
            {
                continue;
            }

            grouped.Remove(biteDot);
            grouped.Remove(windDot);
            StatusSnapshot nextToExpire = biteDot.Remaining <= windDot.Remaining ? biteDot : windDot;
            grouped.Add(new StatusSnapshot(
                TimelineKey.ForStatus(BrdCombinedDotKeyId, targetDots.Key.Owner, targetDots.Key.Target),
                BrdCombinedDotIconId,
                nextToExpire.Remaining,
                Math.Max(biteDot.Duration, windDot.Duration),
                nextToExpire.BadgeText));
        }

        return grouped;
    }

    private readonly record struct StatusSnapshot(
        TimelineKey Key,
        uint IconId,
        float Remaining,
        float Duration,
        string? BadgeText);
}
