using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Logging;
using ZDs.Config;
using ZDs.Helpers;

namespace ZDs.Windows
{
    internal class TimelineWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;
        
        private bool userChangedSize = false;
        private bool userChangedPosition = false;

        private DrawHelper.WindowInteractionState _dragdata = new DrawHelper.WindowInteractionState();
        
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
            ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor.Base);

            Flags = _baseFlags;

            if (Config.GeneralConfig.TimelineLocked)
            {
                Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, Config.GeneralConfig.TimelineBorder ? Config.GeneralConfig.TimelineBorderThickness : 0);
        }

        public override void PostDraw()
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
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

            Vector2 defaultSize = new Vector2(Config.CooldownConfig.TimelineIconSize);
            uint defaultTextColor = Config.CooldownConfig.CooldownTextColor.Base;
            uint defaultTextOutlineColor = Config.CooldownConfig.CooldownTextOutlineColor.Base;
            
            // Sort the list by cooldownLeft, reversed if reverseOrder is true
            var sortedList = list
                             .Select(item => new { Item = item, CooldownLeft = item.Cooldown / 10 - (now - item.Time) })
                             .OrderBy(x => Config.GeneralConfig.ReverseIconDrawOrder ? x.CooldownLeft : -x.CooldownLeft)
                             .Select(x => x.Item)
                             .ToList();
            
            foreach (var item in sortedList)
            {
                Vector2 iconSize = defaultSize;
                uint textColor = defaultTextColor;
                uint textOutlineColor = defaultTextOutlineColor;

                double timeSince = now - item.Time;
                double cooldown = item.Cooldown / 10;
                double cooldownLeft = cooldown - timeSince;
                var cooldownFont = Config.CooldownConfig.CooldownFontKey;

                if (Config.CooldownConfig.TimelineThresholdEnabled && cooldownLeft <= Config.CooldownConfig.TimelineThresholdTime)
                {
                    iconSize = new Vector2(Config.CooldownConfig.TimelineThresholdIconSize);
                    textColor = Config.CooldownConfig.CooldownTextThresholdColor.Base;
                    textOutlineColor = Config.CooldownConfig.CooldownTextOutlineThresholdColor.Base;
                    cooldownFont = Config.CooldownConfig.CooldownThresholdFontKey;
                }

                cooldownLeft = Config.CooldownConfig.RoundingMode switch
                {
                    0 => Math.Truncate(cooldownLeft),
                    1 => Math.Floor(cooldownLeft),
                    2 => Math.Ceiling(cooldownLeft),
                    3 => Math.Round(cooldownLeft),
                    var _ => 0
                };

                float dimension = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop ? height : width;
                float posAlongTimeline = GetPos(item.Cooldown / 10, (float)timeSince, dimension);

                float posX = Config.GeneralConfig.TimelineOrientation is Orientation.LeftToRight or Orientation.RightToLeft
                    ? pos.X + posAlongTimeline
                    : pos.X + width / 2f;

                float posY = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop
                    ? pos.Y + posAlongTimeline
                    : pos.Y + height / 2f;

                Vector2 position = new Vector2(posX - iconSize.X / 2f, posY - iconSize.Y / 2f);

                if (timeSince < cooldown)
                {
                    DrawHelper.DrawConditionalIcon(item.IconID, position, iconSize, Config.CooldownConfig.DrawIconBorder, 1, drawList, true);
                    if (Config.CooldownConfig.DrawIconCooldown) DrawHelper.DrawIconCooldown(position, iconSize, cooldown - timeSince, cooldown, drawList);
                    string cooldownString = Config.CooldownConfig.ShowCooldownAsMinutes ? Utils.DurationToString(cooldownLeft) : cooldownLeft.ToString();

                    using (FontsManager.PushFont(cooldownFont))
                    {
                        Vector2 textSize = ImGui.CalcTextSize(cooldownString);
                        Vector2 textPosition = new Vector2(
                            position.X + (iconSize.X - textSize.X) / 2,
                            position.Y + (iconSize.Y - textSize.Y) / 2
                        );

                        DrawHelper.DrawOutlinedText(cooldownString, textPosition, textColor, textOutlineColor, drawList);
                    }
                }
            }
        }


        public float GetPos(float recast, float elapsedTime, float windowSize)
        {
            double recastTime = recast - elapsedTime;
            double time = recastTime > Config.GeneralConfig.TimelineTime ? Config.GeneralConfig.TimelineTime : recastTime;
            double powResult = Math.Pow(time, Config.GeneralConfig.TimelineCompression);
            double denominator = Math.Pow(Config.GeneralConfig.TimelineTime, Config.GeneralConfig.TimelineCompression);
            float pos = (float)(powResult / denominator * windowSize);
            
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
            string maxLabel = Utils.DurationToString(maxTime);
            Vector2 labelSize = ImGui.CalcTextSize(maxLabel);

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
            
            uint gridSegmentTextColor = Config.GridConfig.GridSegmentLabelColor.Base;
            uint gridSegmentTextOutlineColor = Config.GridConfig.GridSegmentLabelOutlineColor.Base;
            
            if (Config.GridConfig.ShowGridCenterLine)
            {
                Vector2 fullPos = ImGui.GetWindowPos();
                Vector2 fullSize = ImGui.GetWindowSize();
                Vector2 start, end;

                if (Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop)
                {
                    float centerX = fullPos.X + fullSize.X / 2f;
                    start = new Vector2(centerX, fullPos.Y);
                    end = new Vector2(centerX, fullPos.Y + fullSize.Y);
                }
                else
                {
                    float centerY = fullPos.Y + fullSize.Y / 2f;
                    start = new Vector2(fullPos.X, centerY);
                    end = new Vector2(fullPos.X + fullSize.X, centerY);
                }

                drawList.AddLine(start, end, lineColor, Config.GridConfig.GridLineWidth);
            }

            if (!Config.GridConfig.GridDrawSegments) { return; }
            
            float dimension = Config.GeneralConfig.TimelineOrientation is Orientation.TopToBottom or Orientation.BottomToTop ? height : width;

            for (int i = 0; i <= maxTime; i++)
            {
                float newPos = GetPos(Config.GeneralConfig.TimelineTime, i, dimension);
                float elapsedTime = maxTime - i;
                
                if (Config.GridConfig.GridSegments.Contains(elapsedTime) && !(elapsedTime > maxTime))
                {
                    if (Config.GridConfig.GridDrawSegmentLines)
                    {
                        var (start, end) = GetGridLinePoints(pos, newPos, width, height, Config.GridConfig.GridLineHeight, Config.GeneralConfig.TimelineOrientation);
                        drawList.AddLine(start, end, lineColor, Config.GridConfig.GridLineWidth);
                    }

                    if (Config.GridConfig.GridShowSecondsText)
                    {
                        string text = Utils.DurationToString(elapsedTime);
                        Vector2 textSize = ImGui.CalcTextSize(text);
                        Vector2 textPos = GetGridTextPosition(pos, newPos, width, height, textSize, Config.GeneralConfig.TimelineOrientation, Config.GridConfig.GridSegmentLabelAnchorBottom, Config.GridConfig.GridSegmentLabelOffset);

                        using (FontsManager.PushFont(Config.GridConfig.GridFontKey))
                        {
                            DrawHelper.DrawOutlinedText(text, textPos, gridSegmentTextColor, gridSegmentTextOutlineColor, drawList);
                        }
                    }
                }
            }
        }
    }
}
