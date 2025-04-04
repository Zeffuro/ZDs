﻿using Dalamud.Interface;
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
            float width = size.X;
            float height = size.Y;
            double now = ImGui.GetTime();

            
            if (Config.GeneralConfig.ShouldClip)
            {
                ClipRect? clipRect = Singletons.Get<ClipRectsHelper>().GetClipRectForArea(pos, new Vector2(width, height));
                
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

                float posX = GetX(item.Cooldown / 10, (float)timeSince, width);
                float posY = height / 2f;
                
                Vector2 position = new Vector2(pos.X + posX - iconSize.X / 2f, pos.Y + posY - iconSize.Y / 2f);

                if (timeSince < cooldown)
                {
                    DrawHelper.DrawIcon(item.IconID, position, iconSize, Config.CooldownConfig.DrawIconBorder, 1, drawList);
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


        public float GetX(float recast, float elapsedTime, float windowWidth)
        {
            double recastTime = recast - elapsedTime;
            double time = recastTime > Config.GeneralConfig.TimelineTime ? Config.GeneralConfig.TimelineTime : recastTime;
            double powResult = Math.Pow(time, Config.GeneralConfig.TimelineCompression);
            double denominator = Math.Pow(Config.GeneralConfig.TimelineTime, Config.GeneralConfig.TimelineCompression);
            
            return (float)(powResult / denominator * (windowWidth - Config.CooldownConfig.TimelineIconSize));
        }
        
        private void DrawGrid()
        {
            if (!Config.GridConfig.ShowGrid) { return; }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            float width = ImGui.GetWindowWidth();
            float height = ImGui.GetWindowHeight();

            
            int maxTime = Config.GeneralConfig.TimelineTime;

            uint lineColor = Config.GridConfig.GridLineColor.Base;
            
            uint gridSegmentTextColor = Config.GridConfig.GridSegmentLabelColor.Base;
            uint gridSegmentTextOutlineColor = Config.GridConfig.GridSegmentLabelOutlineColor.Base;
            
            if (Config.GridConfig.ShowGridCenterLine)
            {
                drawList.AddLine(new Vector2(pos.X, pos.Y + height / 2f), new Vector2(pos.X + width, pos.Y + height / 2f), lineColor, Config.GridConfig.GridLineWidth);
            }

            if (!Config.GridConfig.GridDrawSegments) { return; }

            for (int i = 0; i <= maxTime; i++)
            {
                float x = GetX(Config.GeneralConfig.TimelineTime, i, width);
                float elapsedTime = maxTime - i;
                
                if (Config.GridConfig.GridSegments.Contains(elapsedTime) && !(elapsedTime > maxTime))
                {
                    if (Config.GridConfig.GridDrawSegmentLines)
                    {
                        drawList.AddLine(
                            new Vector2(pos.X + x, pos.Y + height / 2f - Config.GridConfig.GridLineHeight), // Adjust for lineHeight
                            new Vector2(pos.X + x, pos.Y + height / 2f + Config.GridConfig.GridLineHeight), // Adjust for lineHeight
                            lineColor, Config.GridConfig.GridLineWidth);
                    }

                    if (Config.GridConfig.GridShowSecondsText)
                    {
                        string text = Utils.DurationToString(elapsedTime);
                        Vector2 textSize = ImGui.CalcTextSize(text);
                        float textPosX = pos.X + x - textSize.X / 2; // Center the text horizontally
                        float textPosY = Config.GridConfig.GridSegmentLabelAnchorBottom
                            ? pos.Y + height - textSize.Y - Config.GridConfig.GridSegmentLabelOffset
                            : pos.Y + Config.GridConfig.GridSegmentLabelOffset;
                        

                        using (FontsManager.PushFont(Config.GridConfig.GridFontKey))
                        {
                            DrawHelper.DrawOutlinedText(text, new Vector2(textPosX, textPosY), gridSegmentTextColor, gridSegmentTextOutlineColor, drawList);
                        }
                    }
                }
            }
        }
    }
}
