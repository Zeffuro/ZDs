using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Logging;
using ZDs.Config;
using ZDs.Helpers;
using ZDs.Timeline;

namespace ZDs.Windows
{
    internal class TimelineWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;

        private DrawHelper.WindowInteractionState _dragdata = new DrawHelper.WindowInteractionState();
        private ImRaii.ColorDisposable? _windowBackground;
        private ImRaii.StyleDisposable? _windowStyle;
        private readonly TimelineLayoutState _layoutState = new();
        private readonly Dictionary<TimelineItem, Vector2> _farRailPositions = [];

        private ZDsConfig Config => Plugin.Config;

        private ImGuiWindowFlags _baseFlags = ImGuiWindowFlags.NoScrollbar
                                            | ImGuiWindowFlags.NoCollapse
                                            | ImGuiWindowFlags.NoTitleBar
                                            | ImGuiWindowFlags.NoNav
                                            | ImGuiWindowFlags.NoScrollWithMouse
                                            | ImGuiWindowFlags.NoFocusOnAppearing;

        public TimelineWindow(string name) : base(name)
        {
            Flags = _baseFlags;

            Size = Config.GeneralConfig.Size;
            SizeCondition = ImGuiCond.FirstUseEver;

            Position = Config.GeneralConfig.Position;
            PositionCondition = ImGuiCond.FirstUseEver;
        }

        public override void PreDraw()
        {
            ConfigColor bgColor = Config.GeneralConfig.TimelineLocked ? Config.GeneralConfig.TimelineLockedBackgroundColor : Config.GeneralConfig.TimelineUnlockedBackgroundColor;
            Flags = _baseFlags;

            if (Config.GeneralConfig.TimelineLocked)
            {
                Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;
            }

            _windowBackground = ImRaii.PushColor(ImGuiCol.WindowBg, bgColor.Base);
            _windowStyle = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero)
                .Push(
                    ImGuiStyleVar.WindowBorderSize,
                    Config.GeneralConfig.TimelineBorder ? Config.GeneralConfig.TimelineBorderThickness : 0);
        }

        public override void PostDraw()
        {
            _windowStyle?.Dispose();
            _windowStyle = null;
            _windowBackground?.Dispose();
            _windowBackground = null;
        }

        public override void Draw()
        {
            Vector2 windowPos = ImGui.GetWindowPos();
            Vector2 windowSize = ImGui.GetWindowSize();

            // Update the dragdata state based on the current window position, size, and locked state
            _dragdata.Update(windowPos, windowSize, Config.GeneralConfig.TimelineLocked);

            // Apply the position and size from the configuration if the window is not being interacted with
            if (!_dragdata.Hovered && !_dragdata.Dragging)
            {
                ImGui.SetWindowPos(Config.GeneralConfig.Position, ImGuiCond.None);
                ImGui.SetWindowSize(Config.GeneralConfig.Size, ImGuiCond.None);
            }

            // Update configuration with new position/size if the window is being interacted with
            if (_dragdata is { Hovered: true, Dragging: true })
            {
                Config.GeneralConfig.Position = windowPos;
                Config.GeneralConfig.Size = windowSize;
            }

            // Toggle settings window with right-click
            if (_dragdata.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                Plugin.ToggleSettingsWindow();
            }

            IReadOnlyCollection<TimelineItem>? list = TimelineManager.Instance?.Items;
            if (list == null) { return; }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            Vector2 size = ImGui.GetWindowSize();

            float padding = GetPaddingForLabels() * 2;

            if (Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft)
            {
                pos.X += padding / 2f;
                size.X -= padding;
            }
            else if (Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop)
            {
                pos.Y += padding / 2f;
                size.Y -= padding;
            }

            float width = size.X;
            float height = size.Y;
            double now = ImGui.GetTime();


            if (Config.GeneralConfig.ShouldClip)
            {
                ClipRect? clipRect = Singletons.Get<ClipRectsHelper>().GetClipRectForArea(windowPos, windowSize);

                if (clipRect.HasValue)
                {
                    return;
                }
            }

            DrawGrid();

            float dimension = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop
                ? height
                : width;
            DrawTimelineMarkers(list, pos, width, height, dimension);
            IReadOnlyList<TimelineRenderItem> renderItems = TimelineLayoutEngine.Build(list, now, dimension, Config, _layoutState);
            PlayPriorityAlert(renderItems);
            HashSet<TimelineItem> hiddenFarItems = [];
            Dictionary<TimelineItem, int> farStackCounts = [];
            if (Config.LayoutConfig.FarHorizonEnabled && Config.LayoutConfig.FarHorizonRailEnabled)
            {
                DrawFarHorizonRail(renderItems, pos, width, height, now, drawList, hiddenFarItems);
            }
            else
            {
                _farRailPositions.Clear();
                foreach (IGrouping<(TimelineLane Lane, int Offset), TimelineRenderItem> group in renderItems
                             .Where(item => Config.LayoutConfig.FarHorizonEnabled && item.State == TimelinePresentationState.FarHorizon)
                             .GroupBy(item => (
                                 Config.LayoutConfig.SourceLanesEnabled ? item.Lane : TimelineLane.Actions,
                                 (int)MathF.Round(item.PerpendicularOffset))))
                {
                    List<TimelineRenderItem> stacked = group.OrderBy(item => item.Remaining).ToList();
                    if (stacked.Count <= 1)
                    {
                        continue;
                    }

                    TimelineRenderItem representative = stacked[0];
                    farStackCounts[representative.Item] = stacked.Count;
                    foreach (TimelineRenderItem hidden in stacked.Skip(1))
                    {
                        hiddenFarItems.Add(hidden.Item);
                    }
                }
            }

            if (Config.LayoutConfig.DrawCollisionGuides)
            {
                Vector4 guideColor = Config.GridConfig.GridLineColor.Vector;
                guideColor.W *= 0.22f;
                uint packedGuideColor = ImGui.GetColorU32(guideColor);
                foreach (TimelineRenderItem renderItem in renderItems.Where(item => Math.Abs(item.PerpendicularOffset) > 1f))
                {
                    if (hiddenFarItems.Contains(renderItem.Item))
                    {
                        continue;
                    }

                    float guideX = Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft
                        ? pos.X + renderItem.AxisPosition
                        : pos.X + width / 2f + renderItem.PerpendicularOffset;
                    float guideY = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop
                        ? pos.Y + renderItem.AxisPosition
                        : pos.Y + height / 2f + renderItem.PerpendicularOffset;
                    Vector2 iconCenter = new(guideX, guideY);
                    Vector2 axisPoint = Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft
                        ? new Vector2(guideX, pos.Y + height / 2f)
                        : new Vector2(pos.X + width / 2f, guideY);
                    drawList.AddLine(axisPoint, iconCenter, packedGuideColor, 1f);
                }
            }

            DrawNextChargeGhosts(renderItems, pos, width, height, dimension, drawList, hiddenFarItems);

            foreach (TimelineRenderItem renderItem in renderItems)
            {
                TimelineItem item = renderItem.Item;
                if (hiddenFarItems.Contains(item))
                {
                    continue;
                }

                Vector2 iconSize = new(renderItem.IconSize);
                LabelTextConfig textConfig = renderItem.ThresholdActive
                    ? Config.CooldownConfig.CooldownThresholdText
                    : Config.CooldownConfig.CooldownText;

                float posX = Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft
                    ? pos.X + renderItem.AxisPosition
                    : pos.X + width / 2f + renderItem.PerpendicularOffset;

                float posY = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop
                    ? pos.Y + renderItem.AxisPosition
                    : pos.Y + height / 2f + renderItem.PerpendicularOffset;

                Vector2 center = new(posX, posY);
                if (renderItem.TransitionFrom == TimelinePresentationState.FarHorizon &&
                    _farRailPositions.TryGetValue(item, out Vector2 railCenter))
                {
                    float docking = Math.Clamp((float)((now - renderItem.StateEnteredAt) / 0.22d), 0f, 1f);
                    docking = docking * docking * (3f - 2f * docking);
                    center = Vector2.Lerp(railCenter, center, docking);
                    if (docking >= 1f)
                    {
                        _farRailPositions.Remove(item);
                    }
                }
                else if (renderItem.State != TimelinePresentationState.FarHorizon)
                {
                    _farRailPositions.Remove(item);
                }

                Vector2 position = center - iconSize / 2f;
                bool showIconDetails = renderItem.State != TimelinePresentationState.FarHorizon ||
                                       !Config.LayoutConfig.FarHorizonSimpleIcons;

                if (item.IsCompleted)
                {
                    DrawFinishAnimation(item, position, iconSize, renderItem.Priority, now, drawList);
                    continue;
                }

                if (renderItem.Elapsed < renderItem.Duration)
                {
                    DrawHelper.DrawConditionalIcon(
                        item.IconID,
                        position,
                        iconSize,
                        false,
                        1,
                        drawList,
                        true,
                        item.Key.SourceKind == TimelineSourceKind.Status);
                    if (showIconDetails && Config.CooldownConfig.DrawIconCooldown)
                    {
                        DrawHelper.DrawIconCooldown(position, iconSize, renderItem.Remaining, renderItem.Duration, drawList);
                    }
                    if (Config.CooldownConfig.DrawIconBorder)
                    {
                        DrawHelper.DrawIconBorder(position, iconSize, 1f, drawList);
                    }
                    DrawPriorityEffect(position, iconSize, renderItem.Priority, now, drawList);
                    if (showIconDetails && Config.CooldownConfig.ShowChargePips)
                    {
                        DrawChargePips(item, position, iconSize, drawList);
                    }
                    if (showIconDetails && !string.IsNullOrEmpty(item.BadgeText))
                    {
                        DrawTargetBadge(
                            item.BadgeText,
                            position,
                            iconSize,
                            Config.SourcesConfig.DotTargetLetterCorner,
                            drawList);
                    }
                    if (farStackCounts.TryGetValue(item, out int stackCount))
                    {
                        DrawStackBadge(stackCount, position, iconSize, drawList);
                    }
                    if (showIconDetails)
                    {
                        string cooldownString = Config.CooldownConfig.ShowCooldownAsMinutes
                            ? Utils.DurationToString(renderItem.DisplayRemaining)
                            : renderItem.DisplayRemaining.ToString();

                        using (FontsManager.PushFont(textConfig.FontFamily, textConfig.FontSize))
                        {
                            Vector2 textSize = ImGui.CalcTextSize(cooldownString);
                            Vector2 textPosition = new Vector2(
                                position.X + (iconSize.X - textSize.X) / 2,
                                position.Y + (iconSize.Y - textSize.Y) / 2
                            );

                            DrawHelper.DrawLabelText(
                                cooldownString,
                                textPosition,
                                textConfig.Color.Base,
                                textConfig.EffectColor.Base,
                                textConfig.Effect,
                                drawList,
                                textConfig.ShadowOffset);
                        }
                    }

                    bool priorityOwnsThreshold = renderItem.Priority != TimelinePriority.Normal &&
                                                 Config.AbilitiesConfig.PriorityRules.UsePriorityStyleForThreshold;
                    if (renderItem.ThresholdActive && Config.CooldownConfig.TimelineThresholdAnts && !priorityOwnsThreshold)
                    {
                        DrawAntsBorder(position, iconSize, 1f, drawList, true);
                    }
                }
            }
        }

        private void DrawFinishAnimation(
            TimelineItem item,
            Vector2 position,
            Vector2 iconSize,
            TimelinePriority priority,
            double now,
            ImDrawListPtr drawList)
        {
            CooldownConfig config = Config.CooldownConfig;
            if (!item.CompletionTime.HasValue)
            {
                return;
            }

            float finishDuration = config.FinishAnimation == CooldownFinishAnimation.None ? 0f : config.FinishAnimationDuration;
            float readyDuration = Config.LayoutConfig.ReadyLandingEnabled ? Config.LayoutConfig.ReadyLandingDuration : 0f;
            float duration = Math.Max(0.1f, Math.Max(finishDuration, readyDuration));
            float progress = Math.Clamp((float)((now - item.CompletionTime.Value) / duration), 0f, 1f);
            float alpha = 1f;
            float scale = 1f;

            float exitFade = progress <= 0.75f ? 1f : 1f - (progress - 0.75f) / 0.25f;
            float pulse = 0.5f + 0.5f * MathF.Sin((float)now * config.FinishAnimationSpeed * MathF.Tau);

            switch (config.FinishAnimation)
            {
                case CooldownFinishAnimation.None:
                    alpha = exitFade;
                    break;
                case CooldownFinishAnimation.Fade:
                    alpha = 1f - progress;
                    scale = 1f - progress * 0.08f;
                    break;
                case CooldownFinishAnimation.Pop:
                    alpha = exitFade;
                    scale = 1f + MathF.Sin(progress * MathF.PI) * 0.18f;
                    break;
                case CooldownFinishAnimation.Pulse:
                    alpha = exitFade * (0.72f + pulse * 0.28f);
                    scale = 1f + pulse * 0.08f;
                    break;
                case CooldownFinishAnimation.Ants:
                    alpha = exitFade;
                    break;
                case CooldownFinishAnimation.Breathe:
                    alpha = exitFade;
                    break;
            }

            Vector2 animatedSize = iconSize * scale;
            Vector2 animatedPosition = position - (animatedSize - iconSize) / 2f;
            DrawHelper.DrawConditionalIcon(
                item.IconID,
                animatedPosition,
                animatedSize,
                config.DrawIconBorder,
                alpha,
                drawList,
                true,
                item.Key.SourceKind == TimelineSourceKind.Status);

            if (config.FinishAnimation is CooldownFinishAnimation.None or CooldownFinishAnimation.Fade)
            {
                return;
            }

            AbilityPriorityConfig priorityConfig = Config.AbilitiesConfig.PriorityRules;
            Vector4 finishAccent = priority != TimelinePriority.Normal && priorityConfig.UsePriorityColorForFinish
                ? priorityConfig.GetColor(priority)
                : config.FinishAnimationColor.Vector;
            Vector4 accent = finishAccent;
            accent.W *= alpha;
            if (config.FinishAnimation == CooldownFinishAnimation.Breathe)
            {
                accent.W *= 0.4f + pulse * 0.6f;
            }
            uint accentColor = ImGui.GetColorU32(accent);

            if (config.FinishAnimation == CooldownFinishAnimation.Ants)
            {
                DrawAntsBorder(animatedPosition, animatedSize, alpha, drawList, accentOverride: finishAccent);
                return;
            }

            int borderLayers = config.FinishAnimation switch
            {
                CooldownFinishAnimation.Pop => 2,
                CooldownFinishAnimation.Pulse => 1 + (int)MathF.Round(pulse * 2f),
                _ => 1,
            };
            using var clip = new ImGuiClipRectScope(Vector2.NegativeInfinity, Vector2.One * float.MaxValue);
            for (int i = 0; i < borderLayers; i++)
            {
                Vector2 offset = new Vector2(i);
                drawList.AddRect(animatedPosition - offset, animatedPosition + animatedSize + offset, accentColor);
            }
        }

        private void DrawAntsBorder(
            Vector2 position,
            Vector2 size,
            float alpha,
            ImDrawListPtr drawList,
            bool thresholdStyle = false,
            Vector4? accentOverride = null)
        {
            CooldownConfig config = Config.CooldownConfig;
            Vector4 accent = accentOverride ?? (thresholdStyle
                ? config.TimelineThresholdAntColor.Vector
                : config.FinishAnimationColor.Vector);
            accent.W *= alpha;
            uint accentColor = ImGui.GetColorU32(accent);
            uint shadowColor = ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.8f * alpha));
            int thickness = thresholdStyle ? config.TimelineThresholdAntThickness : config.FinishAnimationThickness;
            float dashLength = thresholdStyle ? config.TimelineThresholdAntDashLength : config.FinishAnimationDashLength;
            float speed = thresholdStyle ? config.TimelineThresholdAntSpeed : config.FinishAnimationSpeed;

            DrawHelper.DrawAnts(
                position,
                size,
                shadowColor,
                thickness + 2,
                dashLength,
                speed,
                drawList);
            DrawHelper.DrawAnts(
                position,
                size,
                accentColor,
                thickness,
                dashLength,
                speed,
                drawList);
        }

        private static void DrawChargePips(TimelineItem item, Vector2 position, Vector2 size, ImDrawListPtr drawList)
        {
            int maximum = Math.Clamp(item.MaxCharges, 0, 8);
            if (maximum <= 1)
            {
                return;
            }

            int available = Math.Clamp(item.ActiveCharges, 0, maximum);
            float radius = Math.Clamp(size.X / (maximum * 5f), 1.75f, 3f);
            float gap = radius * 2.8f;
            float totalWidth = gap * (maximum - 1);
            Vector2 first = new(position.X + size.X / 2f - totalWidth / 2f, position.Y + size.Y - radius - 2f);
            uint outline = ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.9f));
            uint filled = ImGui.GetColorU32(ColorWithAlpha(KnownColor.White, 0.95f));
            uint empty = ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.25f, 0.9f));

            for (int index = 0; index < maximum; index++)
            {
                Vector2 center = first + new Vector2(gap * index, 0f);
                drawList.AddCircleFilled(center, radius + 1f, outline);
                drawList.AddCircleFilled(center, radius, index < available ? filled : empty);
            }
        }

        private static void DrawTargetBadge(
            string text,
            Vector2 iconPosition,
            Vector2 iconSize,
            TargetLetterCorner corner,
            ImDrawListPtr drawList)
        {
            Vector2 textSize = ImGui.CalcTextSize(text);
            Vector2 offset = corner switch
            {
                TargetLetterCorner.TopRight => new Vector2(iconSize.X - textSize.X - 2f, 1f),
                TargetLetterCorner.BottomLeft => new Vector2(2f, iconSize.Y - textSize.Y - 1f),
                TargetLetterCorner.BottomRight => new Vector2(iconSize.X - textSize.X - 2f, iconSize.Y - textSize.Y - 1f),
                _ => new Vector2(2f, 1f),
            };
            Vector2 textPosition = iconPosition + offset;
            uint shadow = ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.95f));
            uint foreground = ImGui.GetColorU32(KnownColor.White.Vector());
            drawList.AddText(textPosition + Vector2.One, shadow, text);
            drawList.AddText(textPosition, foreground, text);
        }

        private static void DrawStackBadge(int count, Vector2 iconPosition, Vector2 iconSize, ImDrawListPtr drawList)
        {
            string text = $"+{count - 1}";
            Vector2 textSize = ImGui.CalcTextSize(text);
            Vector2 textPosition = iconPosition + new Vector2(iconSize.X - textSize.X - 3f, iconSize.Y - textSize.Y - 2f);
            drawList.AddRectFilled(
                textPosition - new Vector2(2f, 1f),
                textPosition + textSize + new Vector2(2f, 1f),
                ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.88f)),
                3f);
            drawList.AddText(textPosition, ImGui.GetColorU32(KnownColor.White.Vector()), text);
        }

        private void DrawFarHorizonRail(
            IReadOnlyList<TimelineRenderItem> renderItems,
            Vector2 pos,
            float width,
            float height,
            double now,
            ImDrawListPtr drawList,
            HashSet<TimelineItem> hiddenItems)
        {
            List<TimelineRenderItem> entries = renderItems
                .Where(item => item.State == TimelinePresentationState.FarHorizon)
                .OrderBy(item => item.Remaining)
                .ToList();

            HashSet<TimelineItem> liveItems = renderItems.Select(item => item.Item).ToHashSet();
            foreach (TimelineItem stale in _farRailPositions.Keys.Where(item => !liveItems.Contains(item)).ToArray())
            {
                _farRailPositions.Remove(stale);
            }

            foreach (TimelineRenderItem entry in entries)
            {
                hiddenItems.Add(entry.Item);
            }

            if (entries.Count == 0)
            {
                return;
            }

            bool vertical = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop;
            bool afterAxis = Config.LayoutConfig.FarHorizonRailDock == FarHorizonRailDockSide.RightOrBottom;
            bool timerInIcon = Config.LayoutConfig.FarHorizonRailTimerInIcon;
            float iconExtent = Config.LayoutConfig.FarHorizonIconSize;
            const float railGap = 5f;
            const float tileGap = 5f;
            const float iconBarGap = 2f;
            const float barHeight = 3f;
            const float barTextGap = 1f;
            string[] labels = entries.Select(entry => Utils.DurationToString(entry.Remaining)).ToArray();
            Vector2[] labelSizes = labels.Select(label => ImGui.CalcTextSize(label)).ToArray();
            float textHeight = labelSizes.Max(size => size.Y);
            float tileWidth = Math.Max(iconExtent, labelSizes.Max(size => size.X));
            float tileHeight = iconExtent + iconBarGap + barHeight + (timerInIcon ? 0f : barTextGap + textHeight);
            float span = entries.Count * (vertical ? tileWidth : tileHeight) + (entries.Count - 1) * tileGap;
            Vector2 firstTile;

            if (vertical)
            {
                float railY = afterAxis ? pos.Y + height + railGap : pos.Y - railGap;
                float tileY = afterAxis ? railY + railGap : railY - railGap - tileHeight;
                firstTile = new Vector2(pos.X + width / 2f - span / 2f, tileY);
            }
            else
            {
                float railX = afterAxis ? pos.X + width + railGap : pos.X - railGap;
                float tileX = afterAxis ? railX + railGap : railX - railGap - tileWidth;
                firstTile = new Vector2(tileX, pos.Y + height / 2f - span / 2f);
            }

            uint progressBackground = ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.9f));
            Vector4 progressVector = Config.GridConfig.GridLineColor.Vector;
            progressVector.W = Math.Max(0.65f, progressVector.W);
            uint progressColor = ImGui.GetColorU32(progressVector);
            uint textShadow = ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.95f));

            using var clip = new ImGuiClipRectScope(Vector2.NegativeInfinity, Vector2.One * float.MaxValue);

            for (int index = 0; index < entries.Count; index++)
            {
                TimelineRenderItem renderItem = entries[index];
                Vector2 tilePosition = firstTile;
                if (vertical)
                {
                    tilePosition.X += index * (tileWidth + tileGap);
                }
                else
                {
                    tilePosition.Y += index * (tileHeight + tileGap);
                }

                Vector2 iconPosition = new(
                    tilePosition.X + (tileWidth - iconExtent) / 2f,
                    tilePosition.Y);
                Vector2 iconSize = new(iconExtent);
                Vector2 progressMin = new(
                    iconPosition.X,
                    iconPosition.Y + iconExtent + iconBarGap);
                Vector2 progressMax = progressMin + new Vector2(iconExtent, barHeight);
                drawList.AddRectFilled(progressMin, progressMax, progressBackground);

                float horizon = Math.Max(1f, Config.GeneralConfig.TimelineTime);
                float approachDuration = Math.Max(0.001f, (float)renderItem.Duration - horizon);
                float remainingFraction = Math.Clamp(
                    ((float)renderItem.Remaining - horizon) / approachDuration,
                    0f,
                    1f);
                drawList.AddRectFilled(
                    progressMin,
                    new Vector2(progressMin.X + iconExtent * remainingFraction, progressMax.Y),
                    progressColor);

                DrawHelper.DrawConditionalIcon(
                    renderItem.Item.IconID,
                    iconPosition,
                    iconSize,
                    false,
                    1f,
                    drawList,
                    true,
                    renderItem.Item.Key.SourceKind == TimelineSourceKind.Status);
                if (Config.CooldownConfig.DrawIconBorder)
                {
                    DrawHelper.DrawIconBorder(iconPosition, iconSize, 1f, drawList);
                }
                DrawPriorityEffect(iconPosition, iconSize, renderItem.Priority, now, drawList);

                Vector2 textPosition = timerInIcon
                    ? iconPosition + (iconSize - labelSizes[index]) / 2f
                    : new Vector2(
                        tilePosition.X + (tileWidth - labelSizes[index].X) / 2f,
                        progressMax.Y + barTextGap);
                drawList.AddText(textPosition + Vector2.One, textShadow, labels[index]);
                drawList.AddText(textPosition, ImGui.GetColorU32(KnownColor.White.Vector()), labels[index]);
                _farRailPositions[renderItem.Item] = iconPosition + iconSize / 2f;
            }
        }

        private static Vector4 ColorWithAlpha(KnownColor color, float alpha)
        {
            Vector4 value = color.Vector();
            value.W = alpha;
            return value;
        }

        private void DrawPriorityEffect(
            Vector2 position,
            Vector2 size,
            TimelinePriority priority,
            double now,
            ImDrawListPtr drawList)
        {
            if (priority == TimelinePriority.Normal)
            {
                return;
            }

            AbilityPriorityConfig config = Config.AbilitiesConfig.PriorityRules;
            TimelinePriorityEffect effect = priority == TimelinePriority.Critical
                ? config.CriticalEffect
                : config.ImportantEffect;
            Vector4 color = priority == TimelinePriority.Critical
                ? config.CriticalColor.Vector
                : config.ImportantColor.Vector;
            float breathing = 0.5f + 0.5f * MathF.Sin((float)now * MathF.Tau * 1.25f);
            if (effect == TimelinePriorityEffect.Breathe)
            {
                color.W *= 0.45f + breathing * 0.55f;
            }

            Vector2 outerPosition = position - new Vector2(2f);
            Vector2 outerSize = size + new Vector2(4f);
            if (effect == TimelinePriorityEffect.Ants)
            {
                uint shadow = ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.8f * color.W));
                uint accent = ImGui.GetColorU32(color);
                DrawHelper.DrawAnts(outerPosition, outerSize, shadow, 4, 5f, 1f, drawList);
                DrawHelper.DrawAnts(outerPosition, outerSize, accent, 2, 5f, 1f, drawList);
                return;
            }

            float thickness = priority == TimelinePriority.Critical ? 2f : 1.5f;
            using var clip = new ImGuiClipRectScope(Vector2.NegativeInfinity, Vector2.One * float.MaxValue);
            drawList.AddRect(
                outerPosition,
                outerPosition + outerSize,
                ImGui.GetColorU32(color),
                1f,
                ImDrawFlags.None,
                thickness);
        }

        private void PlayPriorityAlert(IReadOnlyList<TimelineRenderItem> renderItems)
        {
            if (Config.GeneralConfig.Preview)
            {
                return;
            }

            AbilityPriorityRule? alert = renderItems
                .Where(item => item.Item.Key.SourceKind == TimelineSourceKind.Action)
                .Select(item => (Item: item, Rule: Config.AbilitiesConfig.PriorityRules.Find(item.Item.ActionID)))
                .Where(entry => entry.Rule is { SoundId: > 0 } &&
                                (entry.Rule.AlertMoment == AbilityAlertMoment.Ready
                                    ? entry.Item.Crossings.HasFlag(TimelineCrossing.Completed)
                                    : entry.Item.Crossings.HasFlag(TimelineCrossing.EnteredThreshold)))
                .OrderByDescending(entry => entry.Rule!.Priority)
                .Select(entry => entry.Rule)
                .FirstOrDefault();
            if (alert != null)
            {
                UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(alert.SoundId, 1, 16));
            }
        }

        private void DrawNextChargeGhosts(
            IReadOnlyList<TimelineRenderItem> renderItems,
            Vector2 pos,
            float width,
            float height,
            float dimension,
            ImDrawListPtr drawList,
            HashSet<TimelineItem> hiddenItems)
        {
            if (!Config.CooldownConfig.ShowNextChargeGhost ||
                Config.LayoutConfig.ScaleMode == TimelineScaleMode.Percentage)
            {
                return;
            }

            ITimelineScale scale = TimelineScaleFactory.Create(Config);
            float horizon = Math.Max(1, Config.GeneralConfig.TimelineTime);
            foreach (TimelineRenderItem renderItem in renderItems)
            {
                TimelineItem item = renderItem.Item;
                if (hiddenItems.Contains(item) ||
                    item.IsCompleted ||
                    item.MaxCharges <= 1 ||
                    item.ActiveCharges + 1 >= item.MaxCharges)
                {
                    continue;
                }

                float projectedRemaining = (float)(renderItem.Remaining + renderItem.Duration);
                if (projectedRemaining <= 0f || projectedRemaining > horizon)
                {
                    continue;
                }

                float axis = GetAxisPosition(scale, projectedRemaining, (float)renderItem.Duration, dimension);
                float extent = renderItem.IconSize * 0.55f;
                bool vertical = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop;
                float direction = Config.GridConfig.GridSegmentLabelAnchorBottom
                    ? vertical ? 1f : -1f
                    : vertical ? -1f : 1f;
                float separation = Math.Max(
                    Config.CooldownConfig.TimelineIconSize,
                    Config.CooldownConfig.TimelineThresholdIconSize) / 2f + extent / 2f + 5f;
                float centerX = Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft
                    ? pos.X + axis
                    : pos.X + width / 2f + direction * separation;
                float centerY = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop
                    ? pos.Y + axis
                    : pos.Y + height / 2f + direction * separation;
                Vector2 iconSize = new(extent);
                Vector2 iconPosition = new(centerX - extent / 2f, centerY - extent / 2f);
                Vector2 axisPoint = vertical
                    ? new Vector2(pos.X + width / 2f, centerY)
                    : new Vector2(centerX, pos.Y + height / 2f);
                Vector2 connectorEnd = vertical
                    ? new Vector2(centerX - direction * extent / 2f, centerY)
                    : new Vector2(centerX, centerY - direction * extent / 2f);
                using var clip = new ImGuiClipRectScope(Vector2.NegativeInfinity, Vector2.One * float.MaxValue);
                drawList.AddLine(
                    axisPoint,
                    connectorEnd,
                    ImGui.GetColorU32(ColorWithAlpha(KnownColor.White, 0.2f)),
                    1f);
                drawList.AddRectFilled(
                    iconPosition - Vector2.One,
                    iconPosition + iconSize + Vector2.One,
                    ImGui.GetColorU32(ColorWithAlpha(KnownColor.Black, 0.55f)),
                    2f);
                DrawHelper.DrawConditionalIcon(item.IconID, iconPosition, iconSize, false, 0.38f, drawList, true);
                drawList.AddRect(
                    iconPosition - Vector2.One,
                    iconPosition + iconSize + Vector2.One,
                    ImGui.GetColorU32(ColorWithAlpha(KnownColor.White, 0.5f)),
                    2f);
            }
        }

        private void DrawTimelineMarkers(
            IReadOnlyCollection<TimelineItem> items,
            Vector2 pos,
            float width,
            float height,
            float dimension)
        {
            if (!Config.GridConfig.ShowGrid ||
                Config.LayoutConfig.ScaleMode == TimelineScaleMode.Percentage ||
                (!Config.GridConfig.ShowGcdMarker && !Config.GridConfig.ShowCastMarker && !Config.GridConfig.ShowBuffMarkers))
            {
                return;
            }

            ITimelineScale scale = TimelineScaleFactory.Create(Config);
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            foreach (TimelineMarker marker in TimelineMarkerProvider.Get(items, Config.GridConfig, ImGui.GetTime()))
            {
                float axis = GetAxisPosition(scale, marker.Remaining, marker.Duration, dimension);
                uint color = marker.Kind switch
                {
                    TimelineMarkerKind.Gcd => Config.GridConfig.GcdMarkerColor.Base,
                    TimelineMarkerKind.Cast => Config.GridConfig.CastMarkerColor.Base,
                    _ => Config.GridConfig.BuffMarkerColor.Base,
                };
                float halfLength = marker.Kind switch
                {
                    TimelineMarkerKind.Cast => 12f,
                    TimelineMarkerKind.Gcd => 8f,
                    _ => 6f,
                };
                Vector2 start;
                Vector2 end;
                if (Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop)
                {
                    float y = pos.Y + axis;
                    float center = pos.X + width / 2f;
                    start = new Vector2(center - halfLength, y);
                    end = new Vector2(center + halfLength, y);
                }
                else
                {
                    float x = pos.X + axis;
                    float center = pos.Y + height / 2f;
                    start = new Vector2(x, center - halfLength);
                    end = new Vector2(x, center + halfLength);
                }

                drawList.AddLine(start, end, color, 2f);
            }
        }


        public float GetPos(float recast, float elapsedTime, float windowSize, bool smoothArrival = false)
        {
            float remaining = recast - elapsedTime;
            float normalized = TimelineScaleFactory.Create(Config).Map(
                remaining,
                recast,
                Math.Max(1, Config.GeneralConfig.TimelineTime),
                smoothArrival);
            float pos = normalized * windowSize;

            return Config.GeneralConfig.TimelineOrientation switch
            {
                Orientation.RightToLeft or Orientation.BottomToTop => pos,
                Orientation.LeftToRight or Orientation.TopToBottom => windowSize - pos,
                _ => pos // fallback
            };
        }

        private float GetPaddingForLabels()
        {
            int maxTime = Config.GeneralConfig.TimelineTime;
            string maxLabel = Config.LayoutConfig.ScaleMode == TimelineScaleMode.Percentage
                ? "100%"
                : Utils.DurationToString(maxTime);
            Vector2 labelSize;
            using (FontsManager.PushFont(Config.GridConfig.GridLabelText.FontFamily, Config.GridConfig.GridLabelText.FontSize))
            {
                labelSize = ImGui.CalcTextSize(maxLabel);
            }

            float basePadding = Config.GridConfig.GridSegmentLabelOffset + 5;

            return Config.GeneralConfig.TimelineOrientation switch
            {
                Orientation.LeftToRight or Orientation.RightToLeft => labelSize.X + basePadding,
                Orientation.TopToBottom or Orientation.BottomToTop => labelSize.Y + basePadding,
                _ => 0,
            };
        }

        private (Vector2 start, Vector2 end) GetGridLinePoints(Vector2 pos, float newPos, float width, float height, float lineHeight, Orientation orientation)
        {
            return orientation switch
            {
                Orientation.LeftToRight or Orientation.RightToLeft => (
                    new Vector2(pos.X + newPos, pos.Y + height / 2f - lineHeight),
                    new Vector2(pos.X + newPos, pos.Y + height / 2f + lineHeight)
                ),
                Orientation.TopToBottom or Orientation.BottomToTop => (
                    new Vector2(pos.X + width / 2f - lineHeight, pos.Y + newPos),
                    new Vector2(pos.X + width / 2f + lineHeight, pos.Y + newPos)
                ),
                _ => (
                    new Vector2(pos.X + newPos, pos.Y + height / 2f - lineHeight),
                    new Vector2(pos.X + newPos, pos.Y + height / 2f + lineHeight)
                )
            };
        }

        private Vector2 GetGridTextPosition(Vector2 pos, float newPos, float width, float height, Vector2 textSize, Orientation orientation, bool anchorBottom, float offset)
        {
            float gridLineHeight = Config.GridConfig.GridLineHeight;
            return orientation switch
            {
                Orientation.LeftToRight or Orientation.RightToLeft => new Vector2(
                    pos.X + newPos - textSize.X / 2,
                    anchorBottom ? (pos.Y + height / 2f + gridLineHeight + offset) : ((pos.Y + height / 2f - gridLineHeight - offset) - textSize.Y)
                ),

                Orientation.TopToBottom or Orientation.BottomToTop => new Vector2(
                    anchorBottom ? (pos.X + width / 2f - gridLineHeight - offset) - textSize.X : (pos.X + width / 2f + gridLineHeight + offset),
                    pos.Y + newPos - textSize.Y / 2
                ),

                _ => new Vector2(
                    pos.X + newPos - textSize.X / 2,
                    anchorBottom ? pos.Y + height - textSize.Y - offset : pos.Y + offset
                )
            };
        }

        private void DrawGrid()
        {
            if (!Config.GridConfig.ShowGrid) { return; }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            Vector2 size = ImGui.GetWindowSize();

            float padding = GetPaddingForLabels() * 2;

            if (Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft)
            {
                pos.X += padding / 2f;
                size.X -= padding;
            }
            else if (Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop)
            {
                pos.Y += padding / 2f;
                size.Y -= padding;
            }

            float width = size.X;
            float height = size.Y;

            int maxTime = Config.GeneralConfig.TimelineTime;

            uint lineColor = Config.GridConfig.GridLineColor.Base;

            ITimelineScale scale = TimelineScaleFactory.Create(Config);
            DrawScaleBands(drawList, pos, width, height, maxTime, scale);

            if (Config.GridConfig.ShowGridCenterLine)
            {
                Vector2 start, end;

                if (Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop)
                {
                    float centerX = pos.X + width / 2f;
                    start = new Vector2(centerX, pos.Y);
                    end = new Vector2(centerX, pos.Y + height);
                }
                else
                {
                    float centerY = pos.Y + height / 2f;
                    start = new Vector2(pos.X, centerY);
                    end = new Vector2(pos.X + width, centerY);
                }

                drawList.AddLine(start, end, lineColor, Config.GridConfig.GridLineWidth);
            }

            if (!Config.GridConfig.GridDrawSegments) { return; }

            float dimension = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop ? height : width;

            bool percentageMovement = Config.LayoutConfig.ScaleMode == TimelineScaleMode.Percentage;
            List<(float Remaining, float Duration, string Label)> markers = percentageMovement
                ?
                [
                    (0f, 1f, "0%"),
                    (0.25f, 1f, "25%"),
                    (0.5f, 1f, "50%"),
                    (0.75f, 1f, "75%"),
                    (1f, 1f, "100%"),
                ]
                : GetGridSegments(scale, maxTime, dimension)
                    .Select(value => (
                        value,
                        (float)maxTime,
                        Config.GridConfig.GridSegmentLabelsFormatDuration ? Utils.DurationToString(value) : value.ToString()))
                    .ToList();

            List<(float Start, float End)> labelSpans = [];
            bool vertical = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop;
            foreach ((float remaining, float duration, string label) in markers)
            {
                float newPos = GetAxisPosition(scale, remaining, duration, dimension);
                if (Config.GridConfig.GridDrawSegmentLines)
                {
                    var (start, end) = GetGridLinePoints(pos, newPos, width, height, Config.GridConfig.GridLineHeight, Config.GeneralConfig.TimelineOrientation);
                    drawList.AddLine(start, end, lineColor, Config.GridConfig.GridLineWidth);
                }

                if (Config.GridConfig.GridShowSecondsText)
                {
                    LabelTextConfig labelText = Config.GridConfig.GridLabelText;
                    using (FontsManager.PushFont(labelText.FontFamily, labelText.FontSize))
                    {
                        Vector2 textSize = ImGui.CalcTextSize(label);
                        Vector2 textPos = GetGridTextPosition(pos, newPos, width, height, textSize, Config.GeneralConfig.TimelineOrientation, Config.GridConfig.GridSegmentLabelAnchorBottom, Config.GridConfig.GridSegmentLabelOffset);
                        float spanStart = vertical ? textPos.Y : textPos.X;
                        float spanEnd = spanStart + (vertical ? textSize.Y : textSize.X);
                        if (Config.GridConfig.GridAdaptiveLabels &&
                            labelSpans.Any(span => spanEnd + 3f >= span.Start && spanStart - 3f <= span.End))
                        {
                            continue;
                        }

                        labelSpans.Add((spanStart, spanEnd));
                        DrawHelper.DrawLabelText(
                            label,
                            textPos,
                            labelText.Color.Base,
                            labelText.EffectColor.Base,
                            labelText.Effect,
                            drawList,
                            labelText.ShadowOffset);
                    }
                }
            }
        }

        private void DrawScaleBands(
            ImDrawListPtr drawList,
            Vector2 pos,
            float width,
            float height,
            int maxTime,
            ITimelineScale scale)
        {
            if (!Config.LayoutConfig.ScaleBandsEnabled || maxTime <= 0)
            {
                return;
            }

            bool percentageMovement = Config.LayoutConfig.ScaleMode == TimelineScaleMode.Percentage;
            List<float> boundaries = percentageMovement ? [0f, 0.25f, 0.5f, 0.75f, 1f] : new List<float> { 0f };
            if (!percentageMovement)
            {
                float bandDimension = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop
                    ? height
                    : width;
                foreach (float segment in GetGridSegments(scale, maxTime, bandDimension))
                {
                    if (segment > 0 && segment < maxTime && !boundaries.Contains(segment))
                    {
                        boundaries.Add(segment);
                    }
                }
                boundaries.Add(maxTime);
                boundaries.Sort();
            }

            float scaleDuration = percentageMovement ? 1f : maxTime;

            float dimension = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop
                ? height
                : width;
            for (int index = 0; index < boundaries.Count - 1; index++)
            {
                float first = GetAxisPosition(scale, boundaries[index], scaleDuration, dimension);
                float second = GetAxisPosition(scale, boundaries[index + 1], scaleDuration, dimension);
                float low = Math.Min(first, second);
                float high = Math.Max(first, second);
                uint color = index % 2 == 0
                    ? Config.LayoutConfig.ScaleBandColor.Base
                    : Config.LayoutConfig.ScaleBandAlternateColor.Base;

                if (Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop)
                {
                    drawList.AddRectFilled(new Vector2(pos.X, pos.Y + low), new Vector2(pos.X + width, pos.Y + high), color);
                }
                else
                {
                    drawList.AddRectFilled(new Vector2(pos.X + low, pos.Y), new Vector2(pos.X + high, pos.Y + height), color);
                }
            }
        }

        private List<float> GetGridSegments(ITimelineScale scale, int maxTime, float dimension)
        {
            if (!Config.GridConfig.GridAutoSegments)
            {
                return Config.GridConfig.GridSegments
                    .Distinct()
                    .Where(value => value >= 0 && value <= maxTime)
                    .OrderBy(value => value)
                    .ToList();
            }

            float[] choices = [1f, 3f, 5f, 10f, 15f, 20f, 30f, 45f, 60f, 90f, 120f, 180f, 240f, 300f];
            List<float> candidates = choices
                .Where(value => value < maxTime)
                .Append(maxTime)
                .Distinct()
                .Select(value => (Value: value, Axis: GetAxisPosition(scale, value, maxTime, dimension)))
                .OrderBy(point => point.Axis)
                .Select(point => point.Value)
                .ToList();
            List<float> selected = [];
            float minimumSpacing = Math.Clamp(dimension / 7f, 48f, 82f);
            foreach (float value in candidates)
            {
                float axis = GetAxisPosition(scale, value, maxTime, dimension);
                if (selected.Count == 0 || selected.All(existing =>
                        Math.Abs(axis - GetAxisPosition(scale, existing, maxTime, dimension)) >= minimumSpacing))
                {
                    selected.Add(value);
                }
            }

            if (!selected.Contains(maxTime))
            {
                float horizonAxis = GetAxisPosition(scale, maxTime, maxTime, dimension);
                selected.RemoveAll(value =>
                    Math.Abs(horizonAxis - GetAxisPosition(scale, value, maxTime, dimension)) < minimumSpacing);
                selected.Add(maxTime);
            }

            selected.Sort();
            return selected;
        }

        private float GetAxisPosition(ITimelineScale scale, float remaining, float duration, float dimension, bool smoothArrival = false)
        {
            float normalized = scale.Map(
                remaining,
                duration,
                Math.Max(1, Config.GeneralConfig.TimelineTime),
                smoothArrival);
            float position = normalized * dimension;
            return Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.TopToBottom
                ? dimension - position
                : position;
        }
    }
}
