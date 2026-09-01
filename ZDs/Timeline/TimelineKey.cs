using FFXIVClientStructs.FFXIV.Client.Game;

namespace ZDs.Timeline;

public enum TimelineSourceKind
{
    Action = 0,
    Item = 1,
    Status = 2,
    DutyAction = 3,
    PhantomAction = 4,
    LostAction = 5
}

public readonly record struct TimelineKey(
    TimelineSourceKind SourceKind,
    ActionType ActionType,
    uint Id,
    ulong OwnerObjectId = 0,
    ulong TargetObjectId = 0)
{
    public static TimelineKey ForAction(uint actionId) =>
        new(TimelineSourceKind.Action, ActionType.Action, actionId);

    public static TimelineKey ForItem(uint itemId) =>
        new(TimelineSourceKind.Item, ActionType.Item, itemId);

    public static TimelineKey ForDutyAction(uint actionId) =>
        new(TimelineSourceKind.DutyAction, ActionType.Action, actionId);

    public static TimelineKey ForLostAction(uint actionId) =>
        new(TimelineSourceKind.LostAction, ActionType.Action, actionId);

    public static TimelineKey ForPhantomAction(uint actionId) =>
        new(TimelineSourceKind.PhantomAction, ActionType.Action, actionId);

    public static TimelineKey ForStatus(uint statusId, ulong ownerObjectId, ulong targetObjectId) =>
        new(TimelineSourceKind.Status, ActionType.Action, statusId, ownerObjectId, targetObjectId);
}
