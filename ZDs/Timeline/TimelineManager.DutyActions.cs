using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ZDs.Helpers;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaLostAction = Lumina.Excel.Sheets.MYCTemporaryItem;

namespace ZDs.Timeline;

public partial class TimelineManager
{
    private readonly HashSet<uint> _lostActionIds = IDataManager.Get().GetExcelSheet<LuminaLostAction>()
        .Select(item => item.Action.RowId)
        .Where(actionId => actionId != 0)
        .ToHashSet();

    private unsafe void QueueDutyAction(uint actionId, TimelineSourceKind sourceKind)
    {
        if (!TryFindDutyActionSlot(actionId, out _))
        {
            return;
        }

        TimelineKey key = KeyForExtendedAction(sourceKind, actionId);
        double now = ImGui.GetTime();
        _pendingSourceUses[key] = new PendingSourceUse(key, actionId, now + SourceUseRetrySeconds);
        if (TryAddDutyAction(actionId, sourceKind, now))
        {
            _pendingSourceUses.Remove(key);
        }
    }

    private unsafe bool TryAddDutyAction(uint actionId, TimelineSourceKind sourceKind, double now)
    {
        if (!TryFindDutyActionSlot(actionId, out int slot) ||
            SourceKindForDutyAction(actionId) != sourceKind ||
            !_sheet.TryGetRow(actionId, out LuminaAction action) ||
            !TryReadDutyCooldown(slot, out DutyCooldown cooldown) ||
            cooldown.IsReady)
        {
            return false;
        }

        TimelineKey key = KeyForExtendedAction(sourceKind, actionId);
        TimelineItem? existing = _items.FirstOrDefault(item => item.Key == key);
        if (existing is null)
        {
            EnsureItemCapacity();
            existing = new TimelineItem(
                key,
                action.Icon,
                TimelineItemType.OffGCD,
                now - cooldown.ElapsedIntoNextCharge,
                0,
                0,
                cooldown.ChargeTime * 10,
                cooldown.MaxCharges);
            _items.Add(existing);
        }

        ApplyDutyCooldown(existing, cooldown, now);
        SortTimeline();
        return true;
    }

    private unsafe void SynchronizeDutyActions(double now)
    {
        DutyActionManager* manager = DutyActionManager.GetInstanceIfReady();
        RaptureHotbarModule* hotbar = RaptureHotbarModule.Instance();
        if (manager == null || hotbar == null || hotbar->PhantomActionsPresent)
        {
            RemoveSource(TimelineSourceKind.DutyAction);
            RemoveSource(TimelineSourceKind.LostAction);
            return;
        }

        int slotCount = Math.Min(manager->NumValidSlots, (byte)2);
        for (int slot = 0; slot < slotCount; slot++)
        {
            uint actionId = DutyActionManager.GetDutyActionId((ushort)slot);
            TimelineSourceKind sourceKind = SourceKindForDutyAction(actionId);
            if (actionId != 0 && IsSourceEnabled(sourceKind))
            {
                TryAddDutyAction(actionId, sourceKind, now);
            }
        }

        foreach (TimelineItem item in _items.Where(item =>
                     item.Key.SourceKind is TimelineSourceKind.DutyAction or TimelineSourceKind.LostAction).ToList())
        {
            if (!TryFindDutyActionSlot(item.ActionID, out int slot) ||
                SourceKindForDutyAction(item.ActionID) != item.Key.SourceKind ||
                !TryReadDutyCooldown(slot, out DutyCooldown cooldown))
            {
                _items.Remove(item);
                continue;
            }

            if (cooldown.IsReady)
            {
                CompleteAndRemove(item, now);
                continue;
            }

            ApplyDutyCooldown(item, cooldown, now);
        }
    }

    private TimelineSourceKind SourceKindForDutyAction(uint actionId) =>
        _lostActionIds.Contains(actionId) ? TimelineSourceKind.LostAction : TimelineSourceKind.DutyAction;

    private static TimelineKey KeyForExtendedAction(TimelineSourceKind sourceKind, uint actionId) => sourceKind switch
    {
        TimelineSourceKind.LostAction => TimelineKey.ForLostAction(actionId),
        TimelineSourceKind.PhantomAction => TimelineKey.ForPhantomAction(actionId),
        _ => TimelineKey.ForDutyAction(actionId),
    };

    private static unsafe bool TryFindDutyActionSlot(uint actionId, out int slot)
    {
        DutyActionManager* manager = DutyActionManager.GetInstanceIfReady();
        if (manager != null)
        {
            int slotCount = Math.Min(manager->NumValidSlots, (byte)2);
            for (int index = 0; index < slotCount; index++)
            {
                if (DutyActionManager.GetDutyActionId((ushort)index) == actionId)
                {
                    slot = index;
                    return true;
                }
            }
        }

        slot = -1;
        return false;
    }

    private static unsafe bool TryFindPhantomActionSlot(uint actionId, out int slot)
    {
        DutyActionManager* manager = DutyActionManager.GetInstanceIfReady();
        RaptureHotbarModule* hotbar = RaptureHotbarModule.Instance();
        if (manager != null && hotbar != null && hotbar->PhantomActionsPresent)
        {
            for (int index = 0; index < 5; index++)
            {
                if (DutyActionManager.GetDutyActionId((ushort)index) == actionId)
                {
                    slot = index;
                    return true;
                }
            }
        }

        slot = -1;
        return false;
    }

    private void QueuePhantomAction(uint actionId)
    {
        TimelineKey key = TimelineKey.ForPhantomAction(actionId);
        double now = ImGui.GetTime();
        _pendingSourceUses[key] = new PendingSourceUse(key, actionId, now + SourceUseRetrySeconds);
        if (TryAddPhantomAction(actionId, now))
        {
            _pendingSourceUses.Remove(key);
        }
    }

    private unsafe void SynchronizePhantomActions(double now)
    {
        DutyActionManager* manager = DutyActionManager.GetInstanceIfReady();
        RaptureHotbarModule* hotbar = RaptureHotbarModule.Instance();
        if (manager == null || hotbar == null || !hotbar->PhantomActionsPresent)
        {
            RemoveSource(TimelineSourceKind.PhantomAction);
            return;
        }

        HashSet<uint> equipped = [];
        for (int slot = 0; slot < 5; slot++)
        {
            uint actionId = DutyActionManager.GetDutyActionId((ushort)slot);
            if (actionId == 0)
            {
                continue;
            }

            equipped.Add(actionId);
            TryAddPhantomAction(actionId, now);
        }

        foreach (TimelineItem item in _items.Where(item =>
                     item.Key.SourceKind == TimelineSourceKind.PhantomAction).ToList())
        {
            if (!equipped.Contains(item.ActionID) || !TryReadActionCooldown(item.ActionID, out DutyCooldown cooldown))
            {
                _items.Remove(item);
                continue;
            }

            if (cooldown.IsReady)
            {
                CompleteAndRemove(item, now);
            }
            else
            {
                ApplyDutyCooldown(item, cooldown, now);
            }
        }
    }

    private bool TryAddPhantomAction(uint actionId, double now)
    {
        if (!TryFindPhantomActionSlot(actionId, out _) ||
            !_sheet.TryGetRow(actionId, out LuminaAction action) ||
            !TryReadActionCooldown(actionId, out DutyCooldown cooldown) ||
            cooldown.IsReady)
        {
            return false;
        }

        TimelineKey key = TimelineKey.ForPhantomAction(actionId);
        TimelineItem? existing = _items.FirstOrDefault(item => item.Key == key);
        if (existing is null)
        {
            EnsureItemCapacity();
            existing = new TimelineItem(
                key,
                action.Icon,
                TimelineItemType.OffGCD,
                now - cooldown.ElapsedIntoNextCharge,
                0,
                0,
                cooldown.ChargeTime * 10,
                cooldown.MaxCharges);
            _items.Add(existing);
        }

        ApplyDutyCooldown(existing, cooldown, now);
        SortTimeline();
        return true;
    }

    private static bool TryReadActionCooldown(uint actionId, out DutyCooldown cooldown)
    {
        ActionsHelper.RecastInfo recast = new ActionsHelper().GetAdjustedRecastInfo(actionId);
        int maxCharges = Math.Max(1, (int)recast.MaxCharges);
        int currentCharges = Math.Clamp((int)recast.CurrentCharges, 0, maxCharges);
        if (recast.ChargeTime <= 0)
        {
            cooldown = default;
            return false;
        }

        float elapsedIntoNextCharge = Math.Clamp(
            recast.RecastTimeElapsed - currentCharges * recast.ChargeTime,
            0,
            recast.ChargeTime);
        cooldown = new DutyCooldown(
            maxCharges,
            currentCharges,
            recast.ChargeTime,
            elapsedIntoNextCharge,
            currentCharges >= maxCharges);
        return true;
    }

    private static unsafe bool TryReadDutyCooldown(int slot, out DutyCooldown cooldown)
    {
        DutyActionManager* manager = DutyActionManager.GetInstanceIfReady();
        if (manager == null || slot is < 0 or > 1)
        {
            cooldown = default;
            return false;
        }

        RecastDetail recast = manager->Recast[slot];
        int maxCharges = Math.Max(1, (int)manager->MaxCharges[slot]);
        int currentCharges = Math.Clamp(manager->CurCharges[slot], 0, maxCharges);
        float chargeTime = recast.Total > 0 ? recast.Total / maxCharges : 0;
        float elapsedIntoNextCharge = chargeTime > 0
            ? Math.Clamp(recast.Elapsed - currentCharges * chargeTime, 0, chargeTime)
            : 0;
        bool isReady = currentCharges >= maxCharges || (!recast.IsActive && recast.Total <= 0);

        cooldown = new DutyCooldown(maxCharges, currentCharges, chargeTime, elapsedIntoNextCharge, isReady);
        return true;
    }

    private static void ApplyDutyCooldown(TimelineItem item, DutyCooldown cooldown, double now)
    {
        item.MaxCharges = cooldown.MaxCharges;
        item.ActiveCharges = cooldown.CurrentCharges;
        item.Cooldown = cooldown.ChargeTime * 10;
        item.Time = now - cooldown.ElapsedIntoNextCharge;
    }

    private readonly record struct DutyCooldown(
        int MaxCharges,
        int CurrentCharges,
        float ChargeTime,
        float ElapsedIntoNextCharge,
        bool IsReady);
}
