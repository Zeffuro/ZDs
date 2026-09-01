using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ZDs.Config;
using ZDs.Helpers;

namespace ZDs.Timeline;

public enum TimelineMarkerKind
{
    Gcd = 0,
    Cast = 1,
    Buff = 2,
}

public readonly record struct TimelineMarker(TimelineMarkerKind Kind, float Remaining, float Duration);

public static unsafe class TimelineMarkerProvider
{
    public static IReadOnlyList<TimelineMarker> Get(IReadOnlyCollection<TimelineItem> items, GridConfig config, double now)
    {
        List<TimelineMarker> markers = [];
        if (config.ShowGcdMarker)
        {
            AddGcdMarker(markers);
        }

        if (config.ShowCastMarker)
        {
            AddCastMarker(markers);
        }

        if (config.ShowBuffMarkers)
        {
            AddBuffMarkers(items, markers, now);
        }

        return markers;
    }

    private static void AddGcdMarker(List<TimelineMarker> markers)
    {
        const int gcdRecastGroup = 57;
        RecastDetail* detail = ActionManager.Instance()->GetRecastGroupDetail(gcdRecastGroup);
        if (detail == null || !detail->IsActive || detail->Total <= 0f || detail->Total > 5.5f)
        {
            return;
        }

        float remaining = Math.Max(0f, detail->Total - detail->Elapsed);
        if (remaining > 0f)
        {
            markers.Add(new TimelineMarker(TimelineMarkerKind.Gcd, remaining, detail->Total));
        }
    }

    private static void AddCastMarker(List<TimelineMarker> markers)
    {
        IPlayerCharacter? player = IObjectTable.Get().LocalPlayer;
        if (player == null || player.Address == nint.Zero)
        {
            return;
        }

        CastInfo* cast = ((Character*)player.Address)->GetCastInfo();
        if (cast == null || !cast->IsCasting || cast->TotalCastTime <= 0f)
        {
            return;
        }

        float remaining = Math.Max(0f, cast->TotalCastTime - cast->CurrentCastTime);
        if (remaining > 0f)
        {
            markers.Add(new TimelineMarker(TimelineMarkerKind.Cast, remaining, cast->TotalCastTime));
        }
    }

    private static void AddBuffMarkers(
        IReadOnlyCollection<TimelineItem> items,
        List<TimelineMarker> markers,
        double now)
    {
        IPlayerCharacter? player = IObjectTable.Get().LocalPlayer;
        if (player == null)
        {
            return;
        }

        ulong playerId = player.GameObjectId;
        foreach (TimelineItem item in items.Where(item =>
                     !item.IsCompleted &&
                     item.Key.SourceKind == TimelineSourceKind.Status &&
                     item.Key.TargetObjectId == playerId))
        {
            float duration = item.Cooldown / 10f;
            float remaining = Math.Max(0f, duration - (float)(now - item.Time));
            if (remaining > 0f)
            {
                markers.Add(new TimelineMarker(TimelineMarkerKind.Buff, remaining, duration));
            }
        }
    }
}
