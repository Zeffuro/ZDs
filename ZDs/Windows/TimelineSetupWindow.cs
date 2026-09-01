using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ZDs.Config;
using ZDs.Helpers;
using ZDs.Timeline;

namespace ZDs.Windows;

internal sealed class TimelineSetupWindow : Window
{
    private string _selectedPresetId = BuiltInPresetCatalog.RecommendedPresetId;
    private string? _previewPresetId;
    private ZDsConfig? _previewConfig;
    private Orientation? _orientationOverride;
    private bool _trackItems;
    private bool _trackDutyActions;
    private bool _trackLostActions;
    private bool _trackPhantomActions;
    private bool _trackDotsAndDebuffs;
    private bool _trackBuffs;
    private bool _groupBrdDots;

    public TimelineSetupWindow()
        : base("ZDs Timeline Setup###ZDsTimelineSetup")
    {
        IsOpen = ProfileManager.PendingOnboarding != OnboardingAudience.None;
        ShowCloseButton = true;
        Size = new Vector2(620f, 620f);
        SizeCondition = ImGuiCond.Appearing;
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560f, 580f),
            MaximumSize = new Vector2(760f, 900f),
        };

        LoadSourceOptions();
    }

    public void OpenForCurrentConfig()
    {
        _selectedPresetId = BuiltInPresetCatalog.RecommendedPresetId;
        _previewPresetId = null;
        _previewConfig = null;
        _orientationOverride = null;
        LoadSourceOptions();
        IsOpen = true;
    }

    private void LoadSourceOptions()
    {
        _trackItems = Plugin.Config.SourcesConfig.TrackItems;
        _trackDutyActions = Plugin.Config.SourcesConfig.TrackDutyActions;
        _trackLostActions = Plugin.Config.SourcesConfig.TrackLostActions;
        _trackPhantomActions = Plugin.Config.SourcesConfig.TrackPhantomActions;
        _trackDotsAndDebuffs = Plugin.Config.SourcesConfig.TrackDotsAndDebuffs;
        _trackBuffs = Plugin.Config.SourcesConfig.TrackBuffs;
        _groupBrdDots = Plugin.Config.SourcesConfig.GroupBrdDots;
    }

    public override void Draw()
    {
        OnboardingAudience audience = ProfileManager.PendingOnboarding;
        if (audience == OnboardingAudience.None)
        {
            IsOpen = false;
            return;
        }

        if (audience == OnboardingAudience.NewUser)
        {
            ImGui.TextWrapped("Welcome to ZDs. Choose a starting layout; every setting can be changed later.");
        }
        else
        {
            ImGui.TextWrapped("ZDs has new timeline layouts. Nothing changes unless you choose one here.");
            string backupName = ProfileManager.MigrationBackupProfileName ?? "Before timeline update";
            ImGui.TextWrapped($"A copy of your current setup was saved as '{backupName}', so you can switch back from Profiles at any time.");
        }

        ImGui.Spacing();
        DrawPresetSelector();
        ImGui.Spacing();
        DrawDirectionChoice();
        ImGui.Spacing();
        DrawPresetPreview();
        ImGui.Spacing();
        DrawSourceOptions();
        ImGui.Spacing();

        if (ImGui.Button("Use Selected Layout"))
        {
            bool applied = ProfileManager.ApplyBuiltInPreset(
                _selectedPresetId,
                createProfile: audience == OnboardingAudience.ExistingUser);
            if (applied)
            {
                ApplyPreviewOverrides();
                ApplySourceOptions();
                ProfileManager.SaveActive();
                Complete();
                DrawHelper.DrawNotification("Timeline preset applied.", NotificationType.Success);
            }
            else
            {
                DrawHelper.DrawNotification("Could not apply the timeline preset.", NotificationType.Error);
            }
        }

        if (audience == OnboardingAudience.ExistingUser)
        {
            ImGui.SameLine();
            if (ImGui.Button("Keep Current"))
            {
                ApplySourceOptions();
                ProfileManager.SaveActive();
                Complete();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Open Full Settings"))
        {
            ApplySourceOptions();
            ProfileManager.SaveActive();
            Complete();
            Plugin.ToggleSettingsWindow();
        }
    }

    public override void OnClose()
    {
        ApplySourceOptions();
        ProfileManager.SaveActive();
        Complete();
    }

    private void DrawPresetSelector()
    {
        using var table = ImRaii.Table(
            "##PresetChooser",
            2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn("Preset", ImGuiTableColumnFlags.WidthFixed, 190f);
        ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
        foreach (BuiltInPreset preset in BuiltInPresetCatalog.All)
        {
            using var id = ImRaii.PushId(preset.Id);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            bool selected = string.Equals(_selectedPresetId, preset.Id, StringComparison.Ordinal);
            string label = preset.Name;
            if (ImGui.RadioButton(label, selected))
            {
                _selectedPresetId = preset.Id;
            }

            ImGui.TableSetColumnIndex(1);
            ImGui.TextWrapped(preset.Description);
        }
    }

    private void DrawPresetPreview()
    {
        if (!EnsurePreviewConfig())
        {
            return;
        }

        ZDsConfig previewConfig = _previewConfig!;

        Vector2 origin = ImGui.GetCursorScreenPos();
        Vector2 canvasSize = new(Math.Max(300f, ImGui.GetContentRegionAvail().X), 108f);
        Vector2 sourceSize = previewConfig.GeneralConfig.Size;
        sourceSize.X = Math.Max(1f, sourceSize.X);
        sourceSize.Y = Math.Max(1f, sourceSize.Y);
        Vector2 previewBounds = canvasSize - new Vector2(8f);
        float previewScale = Math.Min(previewBounds.X / sourceSize.X, previewBounds.Y / sourceSize.Y);
        Vector2 previewSize = sourceSize * previewScale;
        Vector2 previewOrigin = origin + (canvasSize - previewSize) / 2f;
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        Vector4 backgroundVector = previewConfig.GeneralConfig.TimelineUnlockedBackgroundColor.Vector;
        backgroundVector.W = Math.Max(0.2f, backgroundVector.W);
        uint background = ImGui.GetColorU32(backgroundVector);
        uint line = ImGui.GetColorU32(ColorWithAlpha(KnownColor.CornflowerBlue, 0.65f));
        uint action = ImGui.GetColorU32(KnownColor.DeepSkyBlue.Vector());
        uint threshold = ImGui.GetColorU32(KnownColor.Orange.Vector());
        uint ready = ImGui.GetColorU32(ColorWithAlpha(KnownColor.Orange, 0.55f));
        uint distant = ImGui.GetColorU32(ColorWithAlpha(KnownColor.SlateGray, 0.9f));
        uint ghost = ImGui.GetColorU32(ColorWithAlpha(KnownColor.DeepSkyBlue, 0.4f));

        drawList.AddRectFilled(previewOrigin, previewOrigin + previewSize, background, 4f);
        DrawPreviewGrid(drawList, previewConfig, previewOrigin, previewSize, line);

        float normalRadius = Math.Clamp(previewConfig.CooldownConfig.TimelineIconSize * previewScale / 2f, 3f, 10f);
        float thresholdRadius = Math.Clamp(previewConfig.CooldownConfig.TimelineThresholdIconSize * previewScale / 2f, 4f, 12f);
        float rowDistance = (Math.Max(
            previewConfig.CooldownConfig.TimelineIconSize,
            previewConfig.CooldownConfig.TimelineThresholdIconSize) + previewConfig.LayoutConfig.CollisionSpacing) * previewScale;
        TimelineCollisionMode collisionMode = previewConfig.LayoutConfig.SourceLanesEnabled
            ? previewConfig.LayoutConfig.ActionLaneCollisionMode
            : previewConfig.LayoutConfig.CollisionMode;
        float collisionOffset = collisionMode switch
        {
            TimelineCollisionMode.Fan => -rowDistance,
            TimelineCollisionMode.Rows => -rowDistance,
            TimelineCollisionMode.Compact => -Math.Max(2f, previewConfig.LayoutConfig.CollisionSpacing) * previewScale,
            _ => 0f,
        };
        ITimelineScale scale = TimelineScaleFactory.Create(previewConfig);
        DrawPreviewIcon(drawList, PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 88f, 120f, 0f), normalRadius, action);
        DrawPreviewIcon(drawList, PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 38f, 60f, 0f), normalRadius, action);
        DrawPreviewIcon(drawList, PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 16f, 30f, 0f), normalRadius, action);
        DrawPreviewIcon(drawList, PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 15.4f, 30f, collisionOffset), normalRadius, action);
        DrawPreviewIcon(drawList, PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 6f, 30f, 0f), thresholdRadius, threshold);

        if (previewConfig.CooldownConfig.ShowNextChargeGhost)
        {
            bool vertical = previewConfig.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop;
            float direction = previewConfig.GridConfig.GridSegmentLabelAnchorBottom ? (vertical ? 1f : -1f) : (vertical ? -1f : 1f);
            float ghostOffset = direction * (normalRadius + 7f);
            Vector2 ghostPoint = PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 98f, 60f, ghostOffset);
            Vector2 axisPoint = vertical
                ? new Vector2(previewOrigin.X + previewSize.X / 2f, ghostPoint.Y)
                : new Vector2(ghostPoint.X, previewOrigin.Y + previewSize.Y / 2f);
            drawList.AddLine(axisPoint, ghostPoint, ghost, 1f);
            DrawPreviewIcon(drawList, ghostPoint, Math.Max(3f, normalRadius * 0.55f), ghost);
        }

        if (previewConfig.LayoutConfig.FarHorizonEnabled)
        {
            float radius = Math.Clamp(previewConfig.LayoutConfig.FarHorizonIconSize * previewScale / 2f, 3f, 8f);
            DrawPreviewIcon(drawList, PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 180f, 180f, 0f), radius, distant);
        }

        if (previewConfig.LayoutConfig.ReadyLandingEnabled)
        {
            Vector2 readyPoint = PreviewPoint(previewConfig, scale, previewOrigin, previewSize, 0f, 30f, 0f);
            DrawPreviewIcon(drawList, readyPoint, thresholdRadius, ready);
        }

        ImGui.Dummy(canvasSize);
    }

    private void DrawDirectionChoice()
    {
        if (!EnsurePreviewConfig())
        {
            return;
        }

        if (ImGui.Button("Flip Direction"))
        {
            Orientation orientation = Flip(_previewConfig!.GeneralConfig.TimelineOrientation);
            _previewConfig.GeneralConfig.TimelineOrientation = orientation;
            _orientationOverride = orientation;
        }

        ImGui.SameLine();
        ImGui.TextDisabled(DirectionName(_previewConfig!.GeneralConfig.TimelineOrientation));
    }

    private bool EnsurePreviewConfig()
    {
        BuiltInPreset? preset = BuiltInPresetCatalog.Find(_selectedPresetId);
        if (preset is null)
        {
            return false;
        }

        if (_previewConfig is null || !string.Equals(_previewPresetId, preset.Id, StringComparison.Ordinal))
        {
            _previewConfig = ConfigMigration.CreateNewDefault(preset.Id);
            _previewPresetId = preset.Id;
            _orientationOverride = null;
        }

        return true;
    }

    private static Orientation Flip(Orientation orientation) => orientation switch
    {
        Orientation.LeftToRight => Orientation.RightToLeft,
        Orientation.RightToLeft => Orientation.LeftToRight,
        Orientation.TopToBottom => Orientation.BottomToTop,
        _ => Orientation.TopToBottom,
    };

    private static string DirectionName(Orientation orientation) => orientation switch
    {
        Orientation.LeftToRight => "Left to right",
        Orientation.RightToLeft => "Right to left",
        Orientation.TopToBottom => "Top to bottom",
        _ => "Bottom to top",
    };

    private static void DrawPreviewGrid(
        ImDrawListPtr drawList,
        ZDsConfig config,
        Vector2 origin,
        Vector2 size,
        uint color)
    {
        bool vertical = config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop;
        float center = vertical ? origin.X + size.X / 2f : origin.Y + size.Y / 2f;
        ITimelineScale scale = TimelineScaleFactory.Create(config);
        bool percentage = config.LayoutConfig.ScaleMode == TimelineScaleMode.Percentage;
        List<float> boundaries = percentage
            ? [0f, 0.25f, 0.5f, 0.75f, 1f]
            : [0f, .. config.GridConfig.GridSegments.Where(value => value > 0 && value < config.GeneralConfig.TimelineTime), config.GeneralConfig.TimelineTime];
        boundaries = boundaries.Distinct().OrderBy(value => value).ToList();
        float scaleDuration = percentage ? 1f : config.GeneralConfig.TimelineTime;
        if (config.GridConfig.ShowGrid && config.LayoutConfig.ScaleBandsEnabled)
        {
            for (int index = 0; index < boundaries.Count - 1; index++)
            {
                Vector2 first = PreviewPoint(config, scale, origin, size, boundaries[index], scaleDuration, 0f);
                Vector2 second = PreviewPoint(config, scale, origin, size, boundaries[index + 1], scaleDuration, 0f);
                uint bandColor = index % 2 == 0
                    ? config.LayoutConfig.ScaleBandColor.Base
                    : config.LayoutConfig.ScaleBandAlternateColor.Base;
                if (vertical)
                {
                    drawList.AddRectFilled(
                        new Vector2(origin.X + 2f, Math.Min(first.Y, second.Y)),
                        new Vector2(origin.X + size.X - 2f, Math.Max(first.Y, second.Y)),
                        bandColor);
                }
                else
                {
                    drawList.AddRectFilled(
                        new Vector2(Math.Min(first.X, second.X), origin.Y + 2f),
                        new Vector2(Math.Max(first.X, second.X), origin.Y + size.Y - 2f),
                        bandColor);
                }
            }
        }

        if (config.GridConfig.ShowGrid && config.GridConfig.GridDrawSegments)
        {
            foreach (float marker in boundaries)
            {
                Vector2 point = PreviewPoint(config, scale, origin, size, marker, scaleDuration, 0f);
                if (vertical)
                {
                    drawList.AddLine(new Vector2(center - 10f, point.Y), new Vector2(center + 10f, point.Y), color, 1f);
                }
                else
                {
                    drawList.AddLine(new Vector2(point.X, center - 10f), new Vector2(point.X, center + 10f), color, 1f);
                }
            }
        }

        if (!config.GridConfig.ShowGrid || !config.GridConfig.ShowGridCenterLine)
        {
            return;
        }

        if (vertical)
        {
            drawList.AddLine(new Vector2(center, origin.Y + 10f), new Vector2(center, origin.Y + size.Y - 10f), color, 2f);
        }
        else
        {
            drawList.AddLine(new Vector2(origin.X + 10f, center), new Vector2(origin.X + size.X - 10f, center), color, 2f);
        }
    }

    private static Vector2 PreviewPoint(
        ZDsConfig config,
        ITimelineScale scale,
        Vector2 origin,
        Vector2 size,
        float remaining,
        float duration,
        float perpendicular)
    {
        const float padding = 14f;
        bool vertical = config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop;
        float normalized = scale.Map(remaining, duration, config.GeneralConfig.TimelineTime, true);
        if (config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.TopToBottom)
        {
            normalized = 1f - normalized;
        }

        return vertical
            ? new Vector2(origin.X + size.X / 2f + perpendicular, origin.Y + padding + normalized * (size.Y - padding * 2f))
            : new Vector2(origin.X + padding + normalized * (size.X - padding * 2f), origin.Y + size.Y / 2f + perpendicular);
    }

    private static void DrawPreviewIcon(ImDrawListPtr drawList, Vector2 center, float radius, uint color)
    {
        drawList.AddRectFilled(center - new Vector2(radius), center + new Vector2(radius), color, 3f);
        drawList.AddRect(
            center - new Vector2(radius),
            center + new Vector2(radius),
            ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.8f)),
            3f,
            ImDrawFlags.None,
            1f);
    }

    private static Vector4 ColorWithAlpha(KnownColor color, float alpha)
    {
        Vector4 value = color.Vector();
        value.W = alpha;
        return value;
    }

    private void DrawSourceOptions()
    {
        ImGui.TextUnformatted("Also show");
        using (var table = ImRaii.Table("##SetupSources", 3, ImGuiTableFlags.SizingStretchSame))
        {
            if (table)
            {
                DrawSourceChoice("Items", ref _trackItems);
                DrawSourceChoice("Duty Actions", ref _trackDutyActions);
                DrawSourceChoice("Lost Actions", ref _trackLostActions);
                DrawSourceChoice("Phantom Actions", ref _trackPhantomActions);
                DrawSourceChoice("DoTs and Debuffs", ref _trackDotsAndDebuffs);
                DrawSourceChoice("Buffs", ref _trackBuffs);
            }
        }

        if (_trackDotsAndDebuffs)
        {
            DrawHelper.DrawNestIndicator(1);
            ImGui.Checkbox("Combine BRD DoTs", ref _groupBrdDots);
            DrawHelper.SetTooltip("Show matching BRD DoTs as Iron Jaws.");
        }
        else
        {
            ImGui.Dummy(new Vector2(0f, ImGui.GetFrameHeight()));
        }
    }

    private static void DrawSourceChoice(string label, ref bool enabled)
    {
        ImGui.TableNextColumn();
        ImGui.Checkbox(label, ref enabled);
    }

    private void ApplySourceOptions()
    {
        SourcesConfig sources = Plugin.Config.SourcesConfig;
        sources.TrackItems = _trackItems;
        sources.TrackDutyActions = _trackDutyActions;
        sources.TrackLostActions = _trackLostActions;
        sources.TrackPhantomActions = _trackPhantomActions;
        sources.TrackDotsAndDebuffs = _trackDotsAndDebuffs;
        sources.TrackBuffs = _trackBuffs;
        sources.GroupBrdDots = _groupBrdDots;
    }

    private void ApplyPreviewOverrides()
    {
        if (_orientationOverride.HasValue)
        {
            Plugin.Config.GeneralConfig.TimelineOrientation = _orientationOverride.Value;
        }
    }

    private void Complete()
    {
        Plugin.Config.GeneralConfig.TimelineLocked = true;
        ConfigHelpers.SaveConfig();
        ProfileManager.CompleteOnboarding();
        IsOpen = false;
    }
}
