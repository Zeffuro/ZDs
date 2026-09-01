using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using ZDs.Helpers;
using ZDs.Timeline;

namespace ZDs.Config;

public enum TimelineScaleMode
{
    Power = 0,
    Piecewise = 1,
    Percentage = 2,
}

public enum TimelineCollisionMode
{
    Overlap = 0,
    Fan = 1,
    Rows = 2,
    Compact = 3,
}

public enum FarHorizonRailDockSide
{
    RightOrBottom = 0,
    LeftOrTop = 1,
}

public sealed class TimelineScalePoint
{
    public float Seconds = 10f;
    public float Position = 0.4f;

    public TimelineScalePoint()
    {
    }

    public TimelineScalePoint(float seconds, float position)
    {
        Seconds = seconds;
        Position = position;
    }
}

public sealed class TimelineLayoutConfig : IConfigPage
{
    [JsonIgnore]
    public bool Active { get; set; }

    public string Name => "Layout";

    public TimelineScaleMode ScaleMode = TimelineScaleMode.Power;
    public List<TimelineScalePoint> ScalePoints =
    [
        new(10f, 0.4f),
        new(30f, 0.7f),
        new(60f, 0.9f),
    ];

    public bool ScaleBandsEnabled;
    public ConfigColor ScaleBandColor = new(0.22f, 0.55f, 0.95f, 0.035f);
    public ConfigColor ScaleBandAlternateColor = new(1f, 1f, 1f, 0.015f);

    public bool SourceLanesEnabled;
    public TimelineCollisionMode CollisionMode = TimelineCollisionMode.Overlap;
    public TimelineCollisionMode ActionLaneCollisionMode = TimelineCollisionMode.Fan;
    public TimelineCollisionMode ItemLaneCollisionMode = TimelineCollisionMode.Compact;
    public TimelineCollisionMode StatusLaneCollisionMode = TimelineCollisionMode.Compact;
    public float LaneSpacing = 42f;
    public int CollisionRows = 3;
    public float CollisionSpacing = 8f;
    public bool DrawCollisionGuides;

    public bool FarHorizonEnabled;
    public int FarHorizonIconSize = 30;
    public bool FarHorizonSimpleIcons;
    public bool FarHorizonRailEnabled;
    public FarHorizonRailDockSide FarHorizonRailDock;
    public bool FarHorizonRailTimerInIcon;

    public bool ReadyLandingEnabled;
    public float ReadyLandingDuration = 1f;

    public IConfigPage GetDefault() => new TimelineLayoutConfig();

    public void Normalize(int horizon)
    {
        horizon = Math.Max(1, horizon);
        ScalePoints ??= [];
        ScalePoints = ScalePoints
            .Where(point => point != null)
            .Select(point => new TimelineScalePoint(
                Math.Clamp(point.Seconds, 0.1f, horizon),
                Math.Clamp(point.Position, 0.01f, 0.99f)))
            .OrderBy(point => point.Seconds)
            .Take(3)
            .ToList();

        float previousSeconds = 0f;
        float previousPosition = 0f;
        foreach (TimelineScalePoint point in ScalePoints)
        {
            point.Seconds = Math.Max(point.Seconds, previousSeconds + 0.1f);
            point.Position = Math.Max(point.Position, previousPosition + 0.01f);
            previousSeconds = point.Seconds;
            previousPosition = point.Position;
        }

        ScalePoints.RemoveAll(point => point.Seconds >= horizon || point.Position >= 1f);
        LaneSpacing = Math.Clamp(LaneSpacing, 0f, 200f);
        CollisionRows = Math.Clamp(CollisionRows, 1, 8);
        CollisionSpacing = Math.Clamp(CollisionSpacing, 0f, 50f);
        FarHorizonIconSize = Math.Clamp(FarHorizonIconSize, 16, 100);
        ReadyLandingDuration = Math.Clamp(ReadyLandingDuration, 0.1f, 5f);
    }

    public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
    {
        using var child = ImRaii.Child("##TimelineLayout", size, border);
        if (!child)
        {
            return;
        }

        GeneralConfig general = Plugin.Config.GeneralConfig;
        Orientation oldOrientation = general.TimelineOrientation;
        if (ImGuiEx.EnumCombo("Direction", ref general.TimelineOrientation,
                ["Right to Left", "Left to Right", "Top to Bottom", "Bottom to Top"]))
        {
            bool wasHorizontal = oldOrientation is Orientation.LeftToRight or Orientation.RightToLeft;
            bool isHorizontal = general.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft;
            if (wasHorizontal != isHorizontal)
            {
                (general.Size.X, general.Size.Y) = (general.Size.Y, general.Size.X);
            }
        }

        ImGui.DragInt("Timeline Range", ref general.TimelineTime, 1f, 1, 300, "%d s");
        DrawHelper.SetTooltip("How far ahead the timeline shows cooldowns.");

        int scaleMode = (int)ScaleMode;
        if (ImGui.Combo("Cooldown Movement", ref scaleMode, ["Classic", "Focus Near Ready", "100% to 0%"], 3))
        {
            ScaleMode = (TimelineScaleMode)scaleMode;
        }

        if (ScaleMode == TimelineScaleMode.Power)
        {
            ImGui.DragFloat("Time Compression", ref general.TimelineCompression, 0.01f, 0.01f, 10f, "%.2f");
            DrawHelper.SetTooltip("Lower values give more room to cooldowns near ready.");
        }
        else if (ScaleMode == TimelineScaleMode.Piecewise)
        {
            Normalize(Plugin.Config.GeneralConfig.TimelineTime);
            ImGui.TextWrapped("Give short cooldowns more room near ready.");
            for (int index = 0; index < ScalePoints.Count; index++)
            {
                TimelineScalePoint point = ScalePoints[index];
                using var id = ImRaii.PushId(index);
                ImGui.SetNextItemWidth(130f);
                ImGui.DragFloat("Seconds", ref point.Seconds, 0.5f, 0.1f, Plugin.Config.GeneralConfig.TimelineTime - 0.1f, "%.1f s");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(130f);
                float percentage = point.Position * 100f;
                if (ImGui.DragFloat("Position", ref percentage, 0.5f, 1f, 99f, "%.0f%%"))
                {
                    point.Position = percentage / 100f;
                }
                ImGui.SameLine();
                if (ImGui.Button("Remove"))
                {
                    ScalePoints.RemoveAt(index--);
                }
            }

            if (ScalePoints.Count < 3 && ImGui.Button("Add Time Marker"))
            {
                float seconds = ScalePoints.Count == 0 ? 10f : ScalePoints[^1].Seconds + 20f;
                float position = ScalePoints.Count == 0 ? 0.4f : ScalePoints[^1].Position + 0.2f;
                ScalePoints.Add(new TimelineScalePoint(seconds, position));
            }
        }
        else
        {
            ImGui.TextWrapped("Every cooldown moves from 100% to 0%.");
        }

        ImGui.NewLine();
        ImGui.Checkbox("Alternating Grid Areas", ref ScaleBandsEnabled);
        DrawHelper.SetTooltip("Shade alternating parts of the grid.");
        if (ScaleBandsEnabled)
        {
            DrawHelper.DrawNestIndicator(1);
            DrawHelper.DrawColorSelector("Band Color", ref ScaleBandColor);
            DrawHelper.DrawNestIndicator(1);
            DrawHelper.DrawColorSelector("Alternate Band Color", ref ScaleBandAlternateColor);
        }

        ImGui.NewLine();
        ImGui.Checkbox("Separate Types", ref SourceLanesEnabled);
        DrawHelper.SetTooltip("Put abilities, items, and effects on separate lines.");
        if (SourceLanesEnabled)
        {
            DrawHelper.DrawNestIndicator(1);
            ImGui.DragFloat("Type Spacing", ref LaneSpacing, 1f, 0f, 200f, "%.0f px");

            DrawHelper.DrawNestIndicator(1);
            int actionCollisionMode = (int)ActionLaneCollisionMode;
            if (ImGui.Combo("Abilities##LaneCollision", ref actionCollisionMode, ["Allow overlap", "Spread outward", "Stack outward", "Compact stack"], 4))
            {
                ActionLaneCollisionMode = (TimelineCollisionMode)actionCollisionMode;
            }

            DrawHelper.DrawNestIndicator(1);
            int itemCollisionMode = (int)ItemLaneCollisionMode;
            if (ImGui.Combo("Items##LaneCollision", ref itemCollisionMode, ["Allow overlap", "Spread outward", "Stack outward", "Compact stack"], 4))
            {
                ItemLaneCollisionMode = (TimelineCollisionMode)itemCollisionMode;
            }

            DrawHelper.DrawNestIndicator(1);
            int statusCollisionMode = (int)StatusLaneCollisionMode;
            if (ImGui.Combo("Effects##LaneCollision", ref statusCollisionMode, ["Allow overlap", "Spread outward", "Stack outward", "Compact stack"], 4))
            {
                StatusLaneCollisionMode = (TimelineCollisionMode)statusCollisionMode;
            }
        }

        if (!SourceLanesEnabled)
        {
            ImGui.NewLine();
            int collisionMode = (int)CollisionMode;
            if (ImGui.Combo("When Icons Touch", ref collisionMode, ["Allow overlap", "Spread in one direction", "Stack in rows", "Compact stack"], 4))
            {
                CollisionMode = (TimelineCollisionMode)collisionMode;
            }
            DrawHelper.SetTooltip("Choose how overlapping icons are arranged.");

            if (CollisionMode == TimelineCollisionMode.Overlap)
            {
                CooldownConfig cooldown = Plugin.Config.CooldownConfig;
                DrawHelper.DrawNestIndicator(1);
                ImGui.Checkbox("Offset Cooldown Icons", ref cooldown.CooldownIconOffsetEnabled);
                DrawHelper.SetTooltip("Fan overlapping icons to one side.");
                if (cooldown.CooldownIconOffsetEnabled)
                {
                    DrawHelper.DrawNestIndicator(2);
                    ImGui.DragInt("Offset Amount", ref cooldown.CooldownIconOffset, 1, -50, 50, "%d px");
                    DrawHelper.DrawNestIndicator(2);
                    ImGui.Checkbox("Reverse Offset", ref cooldown.CooldownIconOffsetInverted);
                }
            }
        }

        ImGui.Checkbox("Newer Icons on Top", ref general.ReverseIconDrawOrder);
        DrawHelper.SetTooltip("Draw recently added icons above older ones.");

        bool usesRows = SourceLanesEnabled
            ? ActionLaneCollisionMode != TimelineCollisionMode.Overlap ||
              ItemLaneCollisionMode != TimelineCollisionMode.Overlap ||
              StatusLaneCollisionMode != TimelineCollisionMode.Overlap
            : CollisionMode != TimelineCollisionMode.Overlap;
        if (usesRows)
        {
            DrawHelper.DrawNestIndicator(1);
            ImGui.DragInt("Max Rows", ref CollisionRows, 1, 1, 8);
            DrawHelper.SetTooltip("Extra icons share the last row.");
            DrawHelper.DrawNestIndicator(1);
            ImGui.DragFloat("Row Spacing", ref CollisionSpacing, 1f, 0f, 50f, "%.0f px");
            bool compactOnly = SourceLanesEnabled
                ? ActionLaneCollisionMode == TimelineCollisionMode.Compact &&
                  ItemLaneCollisionMode == TimelineCollisionMode.Compact &&
                  StatusLaneCollisionMode == TimelineCollisionMode.Compact
                : CollisionMode == TimelineCollisionMode.Compact;
            DrawHelper.SetTooltip(compactOnly
                ? "How much of each stacked icon stays visible."
                : "Space between rows.");
            DrawHelper.DrawNestIndicator(1);
            ImGui.Checkbox("Guide Lines", ref DrawCollisionGuides);
        }

        ImGui.NewLine();
        using (ImRaii.Disabled(ScaleMode == TimelineScaleMode.Percentage))
        {
            ImGui.Checkbox("Long Cooldown Area", ref FarHorizonEnabled);
            DrawHelper.SetTooltip("Keep long cooldowns at the far edge.");
            if (FarHorizonEnabled)
            {
                DrawHelper.DrawNestIndicator(1);
                ImGui.DragInt("Long Cooldown Icon Size", ref FarHorizonIconSize, 1, 16, 100);
                DrawHelper.DrawNestIndicator(1);
                ImGui.Checkbox("Simple Long Cooldowns", ref FarHorizonSimpleIcons);
                DrawHelper.SetTooltip("Hide timers and charge details until icons enter the timeline.");
                DrawHelper.DrawNestIndicator(1);
                ImGui.Checkbox("Compact Rail", ref FarHorizonRailEnabled);
                DrawHelper.SetTooltip("Hold long cooldowns in a compact list beside the timeline.");
                if (FarHorizonRailEnabled)
                {
                    DrawHelper.DrawNestIndicator(2);
                    int railSide = (int)FarHorizonRailDock;
                    string[] railSides = general.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft
                        ? ["Right", "Left"]
                        : ["Bottom", "Top"];
                    if (ImGui.Combo("Rail Position", ref railSide, railSides, railSides.Length))
                    {
                        FarHorizonRailDock = (FarHorizonRailDockSide)railSide;
                    }
                    DrawHelper.DrawNestIndicator(2);
                    ImGui.Checkbox("Timer in Icon", ref FarHorizonRailTimerInIcon);
                }
            }
        }

        ImGui.NewLine();
        ImGui.Checkbox("Pause at Ready", ref ReadyLandingEnabled);
        DrawHelper.SetTooltip("Keep ready icons visible briefly.");
        if (ReadyLandingEnabled)
        {
            DrawHelper.DrawNestIndicator(1);
            ImGui.DragFloat("Pause Duration", ref ReadyLandingDuration, 0.05f, 0.1f, 5f, "%.2f s");
        }

        Normalize(general.TimelineTime);
    }
}
