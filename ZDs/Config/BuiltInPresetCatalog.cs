using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ZDs.Helpers;
using ZDs.Timeline;

namespace ZDs.Config;

public sealed record BuiltInPreset(string Id, string Name, string Description, Action<ZDsConfig> Apply);

public static class BuiltInPresetCatalog
{
    public const string RecommendedPresetId = "balanced";
    public const int CurrentRevision = 1;

    private static readonly IReadOnlyList<BuiltInPreset> Presets =
    [
        new(
            RecommendedPresetId,
            "Recommended",
            "A clear all-purpose layout with extra room where cooldowns are nearly ready.",
            ApplyBalanced),
        new(
            "compact",
            "Compact",
            "The recommended layout in less space, with fewer grid labels.",
            ApplyCompact),
        new(
            "vertical-sidecar",
            "Vertical",
            "A tall, narrow layout for the side of your hotbars or job gauge.",
            ApplyVertical),
        new(
            "minimal",
            "Clean",
            "A simple single-line layout with fewer effects and labels.",
            ApplyMinimal),
        new(
            "percentage",
            "Full Cooldown",
            "Every icon travels the same distance, from 100% remaining to ready at 0%.",
            ApplyPercentage),
        new(
            "classic",
            "Classic",
            "The original ZDs look, movement, and font.",
            ApplyClassic),
    ];

    public static IReadOnlyList<BuiltInPreset> All => Presets;

    public static BuiltInPreset? Find(string id) =>
        Presets.FirstOrDefault(preset => string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool ApplyTo(ZDsConfig config, string id)
    {
        BuiltInPreset? preset = Find(id);
        if (preset is null)
        {
            return false;
        }

        preset.Apply(config);
        config.LayoutConfig.Normalize(config.GeneralConfig.TimelineTime);
        return true;
    }

    private static void ApplyBalanced(ZDsConfig config)
    {
        config.GeneralConfig.Size = new Vector2(520f, 160f);
        config.GeneralConfig.TimelineOrientation = Orientation.RightToLeft;
        config.GeneralConfig.TimelineTime = 120;
        config.GeneralConfig.TimelineCompression = 0.3f;
        config.GeneralConfig.TimelineUnlockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0.38f);
        config.GeneralConfig.ReverseIconDrawOrder = false;

        ConfigurePiecewiseScale(config);
        config.LayoutConfig.ScaleBandsEnabled = true;
        config.LayoutConfig.ScaleBandColor = new ConfigColor(0.22f, 0.55f, 0.95f, 0.035f);
        config.LayoutConfig.ScaleBandAlternateColor = new ConfigColor(1f, 1f, 1f, 0.015f);
        config.LayoutConfig.SourceLanesEnabled = false;
        config.LayoutConfig.CollisionMode = TimelineCollisionMode.Rows;
        config.LayoutConfig.ActionLaneCollisionMode = TimelineCollisionMode.Fan;
        config.LayoutConfig.ItemLaneCollisionMode = TimelineCollisionMode.Compact;
        config.LayoutConfig.StatusLaneCollisionMode = TimelineCollisionMode.Compact;
        config.LayoutConfig.LaneSpacing = 54f;
        config.LayoutConfig.CollisionRows = 3;
        config.LayoutConfig.CollisionSpacing = 2f;
        config.LayoutConfig.DrawCollisionGuides = false;
        config.LayoutConfig.FarHorizonEnabled = true;
        config.LayoutConfig.FarHorizonIconSize = 30;
        config.LayoutConfig.FarHorizonSimpleIcons = false;
        config.LayoutConfig.FarHorizonRailEnabled = true;
        config.LayoutConfig.FarHorizonRailDock = FarHorizonRailDockSide.RightOrBottom;
        config.LayoutConfig.FarHorizonRailTimerInIcon = true;
        config.LayoutConfig.ReadyLandingEnabled = true;
        config.LayoutConfig.ReadyLandingDuration = 1.2f;

        config.CooldownConfig.CooldownIconOffsetEnabled = false;
        config.CooldownConfig.DrawIconBorder = true;
        config.CooldownConfig.BorderColor = new Vector4(0f, 0f, 0f, 1f);
        config.CooldownConfig.DrawIconCooldown = true;
        config.CooldownConfig.ShowChargePips = true;
        config.CooldownConfig.ShowNextChargeGhost = true;
        config.CooldownConfig.TimelineIconSize = 40;
        config.CooldownConfig.TimelineThresholdIconSize = 50;
        config.CooldownConfig.FinishAnimation = CooldownFinishAnimation.Pop;
        config.CooldownConfig.FinishAnimationDuration = 1.2f;
        config.CooldownConfig.FinishAnimationSpeed = 1f;
        config.CooldownConfig.FinishAnimationColor = new ConfigColor(1f, 0.78f, 0.12f, 1f);
        config.CooldownConfig.TimelineThresholdEnabled = true;
        config.CooldownConfig.TimelineThresholdTime = 5;
        config.CooldownConfig.TimelineThresholdAnts = true;
        config.CooldownConfig.TimelineThresholdAntSpeed = 1f;
        config.CooldownConfig.TimelineThresholdAntColor = new ConfigColor(1f, 0.78f, 0.12f, 1f);
        config.GridConfig.ShowGrid = true;
        config.GridConfig.ShowGridCenterLine = true;
        config.GridConfig.GridDrawSegments = true;
        config.GridConfig.GridDrawSegmentLines = true;
        config.GridConfig.GridShowSecondsText = true;
        config.GridConfig.GridAdaptiveLabels = true;
        config.GridConfig.GridAutoSegments = false;
        config.GridConfig.ShowGcdMarker = false;
        config.GridConfig.ShowCastMarker = false;
        config.GridConfig.ShowBuffMarkers = false;
        config.GridConfig.GridSegments = [10f, 30f, 60f, 120f];
        config.GridConfig.GridSegmentLabelsFormatDuration = true;
        ConfigureGameFonts(config);
    }

    private static void ApplyCompact(ZDsConfig config)
    {
        ApplyBalanced(config);
        config.GeneralConfig.Size = new Vector2(420f, 96f);
        config.CooldownConfig.TimelineIconSize = 34;
        config.CooldownConfig.TimelineThresholdIconSize = 42;
        config.LayoutConfig.CollisionMode = TimelineCollisionMode.Compact;
        config.LayoutConfig.CollisionRows = 2;
        config.LayoutConfig.CollisionSpacing = 12f;
        config.LayoutConfig.FarHorizonSimpleIcons = true;
        config.LayoutConfig.ReadyLandingDuration = 1f;
        config.CooldownConfig.ShowNextChargeGhost = false;
        config.CooldownConfig.FinishAnimationDuration = 1f;
        config.GridConfig.GridShowSecondsText = false;
    }

    private static void ApplyVertical(ZDsConfig config)
    {
        ApplyBalanced(config);
        config.GeneralConfig.Size = new Vector2(140f, 520f);
        config.GeneralConfig.TimelineOrientation = Orientation.BottomToTop;
        config.CooldownConfig.ShowNextChargeGhost = false;
    }

    private static void ApplyMinimal(ZDsConfig config)
    {
        ApplyBalanced(config);
        config.GeneralConfig.Size = new Vector2(460f, 80f);
        config.LayoutConfig.SourceLanesEnabled = false;
        config.LayoutConfig.CollisionMode = TimelineCollisionMode.Overlap;
        config.LayoutConfig.DrawCollisionGuides = false;
        config.LayoutConfig.FarHorizonEnabled = false;
        config.LayoutConfig.FarHorizonRailEnabled = false;
        config.LayoutConfig.ReadyLandingEnabled = false;
        config.CooldownConfig.FinishAnimation = CooldownFinishAnimation.None;
        config.CooldownConfig.ShowChargePips = false;
        config.CooldownConfig.ShowNextChargeGhost = false;
        config.CooldownConfig.TimelineThresholdEnabled = false;
        config.CooldownConfig.TimelineThresholdAnts = false;
        config.GridConfig.GridShowSecondsText = false;
    }

    private static void ApplyPercentage(ZDsConfig config)
    {
        ApplyBalanced(config);
        config.LayoutConfig.ScaleMode = TimelineScaleMode.Percentage;
        config.LayoutConfig.FarHorizonEnabled = false;
        config.LayoutConfig.FarHorizonRailEnabled = false;
        config.CooldownConfig.ShowNextChargeGhost = false;
    }

    private static void ApplyClassic(ZDsConfig config)
    {
        config.GeneralConfig.Size = new Vector2(400f, 100f);
        config.GeneralConfig.TimelineOrientation = Orientation.RightToLeft;
        config.GeneralConfig.TimelineTime = 120;
        config.GeneralConfig.TimelineCompression = 0.3f;
        config.GeneralConfig.TimelineUnlockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0.75f);
        config.GeneralConfig.ReverseIconDrawOrder = false;

        config.LayoutConfig.ScaleMode = TimelineScaleMode.Power;
        config.LayoutConfig.ScaleBandsEnabled = false;
        config.LayoutConfig.SourceLanesEnabled = false;
        config.LayoutConfig.CollisionMode = TimelineCollisionMode.Overlap;
        config.LayoutConfig.ActionLaneCollisionMode = TimelineCollisionMode.Fan;
        config.LayoutConfig.ItemLaneCollisionMode = TimelineCollisionMode.Compact;
        config.LayoutConfig.StatusLaneCollisionMode = TimelineCollisionMode.Compact;
        config.LayoutConfig.LaneSpacing = 42f;
        config.LayoutConfig.CollisionRows = 3;
        config.LayoutConfig.CollisionSpacing = 8f;
        config.LayoutConfig.DrawCollisionGuides = false;
        config.LayoutConfig.FarHorizonEnabled = false;
        config.LayoutConfig.FarHorizonSimpleIcons = false;
        config.LayoutConfig.FarHorizonRailEnabled = false;
        config.LayoutConfig.ReadyLandingEnabled = false;
        config.CooldownConfig.CooldownIconOffsetEnabled = false;
        config.CooldownConfig.DrawIconBorder = true;
        config.CooldownConfig.BorderColor = new Vector4(0f, 0f, 0f, 1f);
        config.CooldownConfig.DrawIconCooldown = true;
        config.CooldownConfig.ShowChargePips = false;
        config.CooldownConfig.ShowNextChargeGhost = false;
        config.CooldownConfig.TimelineIconSize = 40;
        config.CooldownConfig.TimelineThresholdIconSize = 50;
        config.CooldownConfig.FinishAnimation = CooldownFinishAnimation.None;
        config.CooldownConfig.FinishAnimationDuration = 0.8f;
        config.CooldownConfig.TimelineThresholdEnabled = true;
        config.CooldownConfig.TimelineThresholdTime = 5;
        config.CooldownConfig.TimelineThresholdAnts = false;
        config.GridConfig.ShowGrid = true;
        config.GridConfig.ShowGridCenterLine = true;
        config.GridConfig.GridDrawSegments = true;
        config.GridConfig.GridDrawSegmentLines = true;
        config.GridConfig.GridShowSecondsText = true;
        config.GridConfig.GridAdaptiveLabels = false;
        config.GridConfig.GridAutoSegments = false;
        config.GridConfig.ShowGcdMarker = false;
        config.GridConfig.ShowCastMarker = false;
        config.GridConfig.ShowBuffMarkers = false;
        config.GridConfig.GridSegments = [10f, 30f, 60f, 120f];
        config.GridConfig.GridSegmentLabelsFormatDuration = true;
        config.CooldownConfig.CooldownText.FontFamily = FontsManager.DefaultFontFamily;
        config.CooldownConfig.CooldownText.FontSize = FontsManager.DefaultMediumFontSize;
        config.CooldownConfig.CooldownThresholdText.FontFamily = FontsManager.DefaultFontFamily;
        config.CooldownConfig.CooldownThresholdText.FontSize = FontsManager.DefaultBigFontSize;
        config.GridConfig.GridLabelText.FontFamily = FontsManager.DefaultFontFamily;
        config.GridConfig.GridLabelText.FontSize = FontsManager.DefaultSmallFontSize;
    }

    private static void ConfigurePiecewiseScale(ZDsConfig config)
    {
        config.LayoutConfig.ScaleMode = TimelineScaleMode.Piecewise;
        config.LayoutConfig.ScalePoints =
        [
            new TimelineScalePoint(10f, 0.4f),
            new TimelineScalePoint(30f, 0.7f),
            new TimelineScalePoint(60f, 0.9f),
        ];
    }

    private static void ConfigureGameFonts(ZDsConfig config)
    {
        string cooldownFamily = EnsureGameFont(config, "TrumpGothic");
        string gridFamily = EnsureGameFont(config, "MiedingerMid");

        config.CooldownConfig.CooldownText.FontFamily = cooldownFamily;
        config.CooldownConfig.CooldownText.FontSize = 23;
        config.CooldownConfig.CooldownThresholdText.FontFamily = cooldownFamily;
        config.CooldownConfig.CooldownThresholdText.FontSize = 34;
        config.GridConfig.GridLabelText.FontFamily = gridFamily;
        config.GridConfig.GridLabelText.FontSize = 14;
    }

    private static string EnsureGameFont(ZDsConfig config, string family)
    {
        config.FontConfig.Fonts ??= [];
        FontData font = new(
            family,
            string.Empty,
            FontsManager.DefaultMediumFontSize,
            false,
            false,
            FontSourceKind.Game,
            family);
        string key = FontsManager.GetFontKey(font);
        config.FontConfig.Fonts.TryAdd(key, font);
        return key;
    }
}
