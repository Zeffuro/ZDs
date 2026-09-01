namespace ZDs.Timeline;

public enum TimelineItemType
{
    Action = 0,
    CastStart = 1,
    CastCancel = 2,
    OffGCD = 3,
    AutoAttack = 4,
}

public sealed class TimelineItem
{
    public TimelineKey Key { get; }
    public uint ActionID => Key.Id;
    public uint IconID { get; }
    public TimelineItemType Type { get; }
    public string? BadgeText { get; set; }
    public double Time { get; set; }
    public double TriggeredAt { get; set; }
    public float GCDDuration { get; }
    public float CastTime { get; }
    public float Cooldown { get; set; }
    public int MaxCharges { get; set; }
    public int ActiveCharges { get; set; }
    public double? CompletionTime { get; }
    public bool IsCompleted => CompletionTime.HasValue;

    public TimelineItem(TimelineKey key, uint iconID, TimelineItemType type, double time)
    {
        Key = key;
        IconID = iconID;
        Type = type;
        Time = time;
        TriggeredAt = time;
    }

    public TimelineItem(
        TimelineKey key,
        uint iconID,
        TimelineItemType type,
        double time,
        float gcdDuration,
        float castTime,
        float cooldown,
        int maxCharges)
        : this(key, iconID, type, time)
    {
        GCDDuration = gcdDuration;
        CastTime = castTime;
        Cooldown = cooldown;
        MaxCharges = maxCharges;
        ActiveCharges = maxCharges;
    }

    private TimelineItem(TimelineItem source, double completionTime)
        : this(
            source.Key,
            source.IconID,
            source.Type,
            source.Time,
            source.GCDDuration,
            source.CastTime,
            source.Cooldown,
            source.MaxCharges)
    {
        ActiveCharges = source.ActiveCharges;
        BadgeText = source.BadgeText;
        TriggeredAt = source.TriggeredAt;
        CompletionTime = completionTime;
    }

    public TimelineItem CreateCompletion(double completionTime) => new(this, completionTime);

    public override string ToString() =>
        $"Key={Key}, IconID={IconID}, Type={Type}, Time={Time:F2}, " +
        $"GCDDuration={GCDDuration:F2}, CastTime={CastTime:F2}, Cooldown={Cooldown:F2}, " +
        $"MaxCharges={MaxCharges}, ActiveCharges={ActiveCharges}";
}
