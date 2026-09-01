using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ZDs.Config;

namespace ZDs.Timeline;

public enum TimelinePresentationState
{
    FarHorizon = 0,
    Moving = 1,
    Imminent = 2,
    Completed = 3,
}

[Flags]
public enum TimelineCrossing
{
    None = 0,
    EnteredHorizon = 1,
    EnteredThreshold = 2,
    Completed = 4,
    ChargeRecovered = 8,
}

public enum TimelineLane
{
    Actions = 0,
    Items = 1,
    Statuses = 2,
}

public sealed record TimelineRenderItem(
    TimelineItem Item,
    double Duration,
    double Elapsed,
    double Remaining,
    double DisplayRemaining,
    TimelinePresentationState State,
    TimelineLane Lane,
    TimelinePriority Priority,
    bool ThresholdActive,
    float AxisPosition,
    float PerpendicularOffset,
    float IconSize,
    TimelineCrossing Crossings,
    double StateEnteredAt,
    TimelinePresentationState? TransitionFrom);

public sealed class TimelineLayoutState
{
    private readonly Dictionary<TimelineKey, int> _collisionSlots = [];
    private readonly Dictionary<TimelineItem, TimelineObservation> _observations = [];
    private bool _initialized;
    private bool _sourceLanesEnabled;
    private TimelineCollisionMode _collisionMode;
    private TimelineCollisionMode _actionLaneCollisionMode;
    private TimelineCollisionMode _itemLaneCollisionMode;
    private TimelineCollisionMode _statusLaneCollisionMode;
    private int _collisionRows;
    private int _priorityHash;
    private Orientation _orientation;

    public void Prepare(IReadOnlyCollection<TimelineRenderItem> items, ZDsConfig config)
    {
        if (!_initialized ||
            _sourceLanesEnabled != config.LayoutConfig.SourceLanesEnabled ||
            _collisionMode != config.LayoutConfig.CollisionMode ||
            _actionLaneCollisionMode != config.LayoutConfig.ActionLaneCollisionMode ||
            _itemLaneCollisionMode != config.LayoutConfig.ItemLaneCollisionMode ||
            _statusLaneCollisionMode != config.LayoutConfig.StatusLaneCollisionMode ||
            _collisionRows != config.LayoutConfig.CollisionRows ||
            _priorityHash != config.AbilitiesConfig.PriorityRules.GetLayoutHash() ||
            _orientation != config.GeneralConfig.TimelineOrientation)
        {
            _collisionSlots.Clear();
            _initialized = true;
            _sourceLanesEnabled = config.LayoutConfig.SourceLanesEnabled;
            _collisionMode = config.LayoutConfig.CollisionMode;
            _actionLaneCollisionMode = config.LayoutConfig.ActionLaneCollisionMode;
            _itemLaneCollisionMode = config.LayoutConfig.ItemLaneCollisionMode;
            _statusLaneCollisionMode = config.LayoutConfig.StatusLaneCollisionMode;
            _collisionRows = config.LayoutConfig.CollisionRows;
            _priorityHash = config.AbilitiesConfig.PriorityRules.GetLayoutHash();
            _orientation = config.GeneralConfig.TimelineOrientation;
        }

        HashSet<TimelineKey> liveKeys = items.Select(item => item.Item.Key).ToHashSet();
        foreach (TimelineKey stale in _collisionSlots.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
        {
            _collisionSlots.Remove(stale);
        }


        HashSet<TimelineItem> liveItems = items.Select(item => item.Item).ToHashSet();
        foreach (TimelineItem stale in _observations.Keys.Where(item => !liveItems.Contains(item)).ToArray())
        {
            _observations.Remove(stale);
        }
    }

    public bool TryGetSlot(TimelineItem item, out int slot) => _collisionSlots.TryGetValue(item.Key, out slot);

    public void SetSlot(TimelineItem item, int slot) => _collisionSlots[item.Key] = slot;

    public TimelineRenderItem Observe(TimelineRenderItem item, double now, ZDsConfig config)
    {
        TimelineCrossing crossings = TimelineCrossing.None;
        double enteredAt = now;
        TimelinePresentationState? transitionFrom = null;
        if (_observations.TryGetValue(item.Item, out TimelineObservation? previous))
        {
            bool stateChanged = previous.State != item.State;
            enteredAt = stateChanged ? now : previous.StateEnteredAt;
            transitionFrom = stateChanged ? previous.State : previous.TransitionFrom;
            if (previous.State == TimelinePresentationState.FarHorizon && item.State != TimelinePresentationState.FarHorizon)
            {
                crossings |= TimelineCrossing.EnteredHorizon;
            }

            if (config.CooldownConfig.TimelineThresholdEnabled &&
                previous.Remaining > config.CooldownConfig.TimelineThresholdTime &&
                item.Remaining <= config.CooldownConfig.TimelineThresholdTime)
            {
                crossings |= TimelineCrossing.EnteredThreshold;
            }

            if (previous.Remaining > 0d && item.Remaining <= 0d)
            {
                crossings |= TimelineCrossing.Completed;
            }

            if (item.Item.ActiveCharges > previous.ActiveCharges)
            {
                crossings |= TimelineCrossing.ChargeRecovered;
            }
        }
        else if (item.State == TimelinePresentationState.Completed)
        {
            crossings = TimelineCrossing.Completed;
        }

        float iconSize = item.IconSize;
        if (item.State == TimelinePresentationState.Imminent)
        {
            const double transitionDuration = 0.18d;
            float progress = Math.Clamp((float)((now - enteredAt) / transitionDuration), 0f, 1f);
            progress = progress * progress * (3f - 2f * progress);
            float normalSize = config.CooldownConfig.TimelineIconSize;
            iconSize = normalSize + (config.CooldownConfig.TimelineThresholdIconSize - normalSize) * progress;
        }
        else if (item.State == TimelinePresentationState.Moving && transitionFrom == TimelinePresentationState.FarHorizon)
        {
            const double dockingDuration = 0.22d;
            float progress = Math.Clamp((float)((now - enteredAt) / dockingDuration), 0f, 1f);
            progress = progress * progress * (3f - 2f * progress);
            iconSize = config.LayoutConfig.FarHorizonIconSize +
                       (config.CooldownConfig.TimelineIconSize - config.LayoutConfig.FarHorizonIconSize) * progress;
        }

        _observations[item.Item] = new TimelineObservation(
            item.State,
            item.Remaining,
            enteredAt,
            transitionFrom,
            item.Item.ActiveCharges);
        return item with
        {
            Crossings = crossings,
            StateEnteredAt = enteredAt,
            IconSize = iconSize,
            TransitionFrom = transitionFrom,
        };
    }

    private sealed record TimelineObservation(
        TimelinePresentationState State,
        double Remaining,
        double StateEnteredAt,
        TimelinePresentationState? TransitionFrom,
        int ActiveCharges);
}

public static class TimelineLayoutEngine
{
    public static IReadOnlyList<TimelineRenderItem> Build(
        IReadOnlyCollection<TimelineItem> items,
        double now,
        float dimension,
        ZDsConfig config,
        TimelineLayoutState state)
    {
        ITimelineScale scale = TimelineScaleFactory.Create(config);
        float horizon = Math.Max(1, config.GeneralConfig.TimelineTime);
        bool smoothArrival = config.CooldownConfig.FinishAnimation != CooldownFinishAnimation.None ||
                             config.LayoutConfig.ReadyLandingEnabled;

        List<TimelineRenderItem> result = items
            .Select(item => CreateItem(item, now, dimension, horizon, smoothArrival, scale, config))
            .OrderBy(item => config.GeneralConfig.ReverseIconDrawOrder ? item.Remaining : -item.Remaining)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Item.Key.SourceKind)
            .ThenBy(item => item.Item.Key.Id)
            .ToList();

        state.Prepare(result, config);
        for (int index = 0; index < result.Count; index++)
        {
            result[index] = state.Observe(result[index], now, config);
        }
        ApplyPerpendicularLayout(result, config, state);
        return result;
    }

    private static TimelineRenderItem CreateItem(
        TimelineItem item,
        double now,
        float dimension,
        float horizon,
        bool smoothArrival,
        ITimelineScale scale,
        ZDsConfig config)
    {
        double duration = item.Cooldown / 10d;
        double elapsed = item.IsCompleted ? duration : now - item.Time;
        double remaining = item.IsCompleted ? 0 : duration - elapsed;
        bool threshold = config.CooldownConfig.TimelineThresholdEnabled &&
                         remaining <= config.CooldownConfig.TimelineThresholdTime;
        bool percentageMovement = config.LayoutConfig.ScaleMode == TimelineScaleMode.Percentage;
        TimelinePresentationState state = item.IsCompleted
            ? TimelinePresentationState.Completed
            : !percentageMovement && remaining > horizon
                ? TimelinePresentationState.FarHorizon
                : threshold
                    ? TimelinePresentationState.Imminent
                    : TimelinePresentationState.Moving;

        float iconSize = state == TimelinePresentationState.FarHorizon && config.LayoutConfig.FarHorizonEnabled
            ? config.LayoutConfig.FarHorizonIconSize
            : threshold
                ? config.CooldownConfig.TimelineThresholdIconSize
                : config.CooldownConfig.TimelineIconSize;

        float normalized = scale.Map((float)remaining, (float)duration, horizon, smoothArrival);
        float axisPosition = normalized * dimension;
        if (config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.TopToBottom)
        {
            axisPosition = dimension - axisPosition;
        }

        if (state == TimelinePresentationState.FarHorizon && config.LayoutConfig.FarHorizonEnabled)
        {
            float edgeInset = iconSize / 2f + 6f;
            axisPosition = dimension <= edgeInset * 2f
                ? dimension / 2f
                : Math.Clamp(axisPosition, edgeInset, dimension - edgeInset);
        }

        double displayRemaining = config.CooldownConfig.RoundingMode switch
        {
            0 => Math.Truncate(remaining),
            1 => Math.Floor(remaining),
            2 => Math.Ceiling(remaining),
            3 => Math.Round(remaining),
            _ => 0,
        };

        return new TimelineRenderItem(
            item,
            duration,
            elapsed,
            remaining,
            displayRemaining,
            state,
            LaneFor(item.Key.SourceKind),
            item.Key.SourceKind == TimelineSourceKind.Action
                ? config.AbilitiesConfig.PriorityRules.GetPriority(item.ActionID)
                : TimelinePriority.Normal,
            threshold,
            axisPosition,
            0f,
            iconSize,
            TimelineCrossing.None,
            now,
            null);
    }

    private static TimelineLane LaneFor(TimelineSourceKind sourceKind) => sourceKind switch
    {
        TimelineSourceKind.Item => TimelineLane.Items,
        TimelineSourceKind.Status => TimelineLane.Statuses,
        _ => TimelineLane.Actions,
    };

    private static void ApplyPerpendicularLayout(
        List<TimelineRenderItem> items,
        ZDsConfig config,
        TimelineLayoutState state)
    {
        if (!config.LayoutConfig.SourceLanesEnabled)
        {
            if (config.LayoutConfig.CollisionMode == TimelineCollisionMode.Overlap)
            {
                ApplyLegacyFan(items, config);
            }
            else
            {
                ApplyCollision(
                    items,
                    ActiveCollisionItems(items, config),
                    0f,
                    config.LayoutConfig.CollisionMode,
                    0,
                    config,
                    state);
            }
        }
        else
        {
            Dictionary<TimelineLane, float> laneOffsets = new()
            {
                [TimelineLane.Actions] = 0f,
                [TimelineLane.Items] = -config.LayoutConfig.LaneSpacing,
                [TimelineLane.Statuses] = config.LayoutConfig.LaneSpacing,
            };

            foreach (IGrouping<TimelineLane, TimelineRenderItem> laneGroup in items.GroupBy(item => item.Lane))
            {
                List<TimelineRenderItem> laneItems = ActiveCollisionItems(laneGroup, config);
                ApplyCollision(
                    items,
                    laneItems,
                    laneOffsets[laneGroup.Key],
                    CollisionModeFor(laneGroup.Key, config),
                    RowDirectionFor(laneGroup.Key),
                    config,
                    state);
            }
        }

        if (config.LayoutConfig.FarHorizonEnabled)
        {
            OffsetStateQueue(items, TimelinePresentationState.FarHorizon, config, state);
        }

        if (config.LayoutConfig.ReadyLandingEnabled)
        {
            OffsetStateQueue(items, TimelinePresentationState.Completed, config, state);
        }
    }

    private static List<TimelineRenderItem> ActiveCollisionItems(
        IEnumerable<TimelineRenderItem> items,
        ZDsConfig config) => items
        .Where(item => !(config.LayoutConfig.FarHorizonEnabled && item.State == TimelinePresentationState.FarHorizon))
        .Where(item => !(config.LayoutConfig.ReadyLandingEnabled && item.State == TimelinePresentationState.Completed))
        .OrderByDescending(item => item.Priority)
        .ThenBy(item => item.AxisPosition)
        .ToList();

    private static void ApplyCollision(
        List<TimelineRenderItem> allItems,
        List<TimelineRenderItem> laneItems,
        float laneOffset,
        TimelineCollisionMode collisionMode,
        int rowDirection,
        ZDsConfig config,
        TimelineLayoutState state)
    {
        switch (collisionMode)
        {
            case TimelineCollisionMode.Fan:
                AssignSlots(allItems, laneItems, laneOffset, rowDirection, config, state, fan: true, compact: false);
                break;
            case TimelineCollisionMode.Rows:
                AssignSlots(allItems, laneItems, laneOffset, rowDirection, config, state, fan: false, compact: false);
                break;
            case TimelineCollisionMode.Compact:
                AssignSlots(allItems, laneItems, laneOffset, rowDirection, config, state, fan: false, compact: true);
                break;
            default:
                foreach (TimelineRenderItem item in laneItems)
                {
                    SetOffset(allItems, item, laneOffset);
                }
                break;
        }
    }

    private static void AssignSlots(
        List<TimelineRenderItem> allItems,
        List<TimelineRenderItem> laneItems,
        float laneOffset,
        int rowDirection,
        ZDsConfig config,
        TimelineLayoutState state,
        bool fan,
        bool compact)
    {
        int slotCount = Math.Max(1, config.LayoutConfig.CollisionRows);
        float collisionIconSize = Math.Max(
            config.CooldownConfig.TimelineIconSize,
            config.CooldownConfig.TimelineThresholdIconSize);
        List<(float Start, float End)>[] occupied = Enumerable
            .Range(0, slotCount)
            .Select(_ => new List<(float Start, float End)>())
            .ToArray();

        foreach (TimelineRenderItem item in laneItems)
        {
            if (state.TryGetSlot(item.Item, out int assignedSlot) &&
                assignedSlot >= 0 &&
                assignedSlot < slotCount)
            {
                occupied[assignedSlot].Add(IntervalFor(item));
            }
        }

        foreach (TimelineRenderItem item in laneItems)
        {
            int slot;
            if (state.TryGetSlot(item.Item, out int preferredSlot) &&
                preferredSlot >= 0 &&
                preferredSlot < slotCount)
            {
                slot = preferredSlot;
            }
            else
            {
                slot = -1;
                (float start, float end) = IntervalFor(item);
                for (int candidate = 0; candidate < slotCount; candidate++)
                {
                    if (occupied[candidate].All(interval => end < interval.Start || start > interval.End))
                    {
                        slot = candidate;
                        break;
                    }
                }
            }

            slot = slot < 0 ? slotCount - 1 : slot;
            if (!state.TryGetSlot(item.Item, out _))
            {
                state.SetSlot(item.Item, slot);
                occupied[slot].Add(IntervalFor(item));
            }

            float slotOffset = rowDirection == 0
                ? fan ? -slot : CenteredSlot(slot)
                : rowDirection * slot;
            float rowDistance = compact
                ? Math.Max(2f, config.LayoutConfig.CollisionSpacing)
                : collisionIconSize + config.LayoutConfig.CollisionSpacing;
            SetOffset(allItems, item, laneOffset + slotOffset * rowDistance);
        }
    }

    private static (float Start, float End) IntervalFor(TimelineRenderItem item) =>
        (item.AxisPosition - item.IconSize / 2f, item.AxisPosition + item.IconSize / 2f + 2f);

    private static void ApplyLegacyFan(List<TimelineRenderItem> items, ZDsConfig config)
    {
        if (!config.CooldownConfig.CooldownIconOffsetEnabled)
        {
            return;
        }

        for (int index = 0; index < items.Count; index++)
        {
            int effectiveIndex = config.CooldownConfig.CooldownIconOffsetInverted
                ? index
                : items.Count - 1 - index;
            SetOffset(items, items[index], -config.CooldownConfig.CooldownIconOffset * effectiveIndex);
        }
    }

    private static void OffsetStateQueue(
        List<TimelineRenderItem> items,
        TimelinePresentationState state,
        ZDsConfig config,
        TimelineLayoutState layoutState)
    {
        foreach (IGrouping<TimelineLane, TimelineRenderItem> group in items
                     .Where(item => item.State == state)
                     .OrderBy(item => item.Remaining)
                     .GroupBy(item => config.LayoutConfig.SourceLanesEnabled ? item.Lane : TimelineLane.Actions))
        {
            float laneOffset = config.LayoutConfig.SourceLanesEnabled
                ? group.Key switch
                {
                    TimelineLane.Items => -config.LayoutConfig.LaneSpacing,
                    TimelineLane.Statuses => config.LayoutConfig.LaneSpacing,
                    _ => 0f,
                }
                : 0f;
            ApplyCollision(
                items,
                group
                    .OrderBy(item => layoutState.TryGetSlot(item.Item, out _) ? 0 : 1)
                    .ThenBy(item => item.Remaining)
                    .ToList(),
                laneOffset,
                config.LayoutConfig.SourceLanesEnabled
                    ? CollisionModeFor(group.Key, config)
                    : config.LayoutConfig.CollisionMode,
                config.LayoutConfig.SourceLanesEnabled ? RowDirectionFor(group.Key) : 0,
                config,
                layoutState);
        }
    }

    private static TimelineCollisionMode CollisionModeFor(TimelineLane lane, ZDsConfig config) => lane switch
    {
        TimelineLane.Items => config.LayoutConfig.ItemLaneCollisionMode,
        TimelineLane.Statuses => config.LayoutConfig.StatusLaneCollisionMode,
        _ => config.LayoutConfig.ActionLaneCollisionMode,
    };

    private static int RowDirectionFor(TimelineLane lane) => lane switch
    {
        TimelineLane.Statuses => 1,
        _ => -1,
    };

    private static float CenteredSlot(int slot) => slot == 0
        ? 0f
        : ((slot + 1) / 2) * (slot % 2 == 1 ? -1f : 1f);

    private static void SetOffset(List<TimelineRenderItem> items, TimelineRenderItem item, float offset)
    {
        int index = items.IndexOf(item);
        if (index >= 0)
        {
            items[index] = item with { PerpendicularOffset = offset };
        }
    }
}
