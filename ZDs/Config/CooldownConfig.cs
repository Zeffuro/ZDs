﻿using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ZDs.Helpers;
using Newtonsoft.Json;

namespace ZDs.Config
{
    public class CooldownConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }
        
        public string Name => "Cooldown";
        
        public int TimelineIconSize = 40;
        public bool DrawIconBorder = true;
        public Vector4 BorderColor = new Vector4(0f, 0f, 0f, 1f);

        public bool DrawIconCooldown = true;

        public bool TimelineThresholdEnabled = true;
        public int TimelineThresholdTime = 5;
        public int TimelineThresholdIconSize = 50;
        
        public string CooldownThresholdFontKey = FontsManager.DefaultBigFontKey;
        public int CooldownThresholdFontId = 0;
        
        public ConfigColor CooldownTextThresholdColor = new ConfigColor(1f, 0f, 0f, 1f);
        public ConfigColor CooldownTextOutlineThresholdColor = new ConfigColor(0f, 0f, 0f, 1f);
        
        public string CooldownFontKey = FontsManager.DefaultMediumFontKey;
        public int CooldownFontId = 0;

        public ConfigColor CooldownTextColor = new ConfigColor(1f, 1f, 1f, 1f);
        public ConfigColor CooldownTextOutlineColor = new ConfigColor(0f, 0f, 0f, 1f);

        public int RoundingMode = 2;
        public bool ShowCooldownAsMinutes = true;

        public IConfigPage GetDefault() => new CooldownConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            if (ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), border))
            {
                ImGui.Checkbox("Draw Border", ref DrawIconBorder);
                ImGui.ColorEdit4("Border Color", ref BorderColor, ImGuiColorEditFlags.NoInputs);
                ImGui.NewLine();
                ImGui.DragInt("Icon Size", ref TimelineIconSize);

                ImGui.NewLine();
                
                DrawHelper.DrawFontSelector("Font##Name", ref CooldownFontKey, ref CooldownFontId);
            
                DrawHelper.DrawColorSelector("Cooldown Text Color", ref CooldownTextColor);
                DrawHelper.DrawColorSelector("Cooldown Text Outline Color", ref CooldownTextOutlineColor);
            
                ImGui.NewLine();
            
                ImGui.Checkbox("Draw Cooldown Swipe", ref DrawIconCooldown);
            
                ImGui.NewLine();

                ImGui.Checkbox("Duration in minutes", ref ShowCooldownAsMinutes);
            
                ImGui.Combo("Rounding Mode", ref RoundingMode, new[] {"Truncate", "Floor", "Ceil", "Round"}, 4, 4);

                ImGui.NewLine();

                ImGui.Checkbox("Threshold Enabled", ref TimelineThresholdEnabled);

                if (TimelineThresholdEnabled)
                {
                    DrawHelper.DrawNestIndicator(1);
                    ImGui.DragInt("Threshold in seconds", ref TimelineThresholdTime);
                    DrawHelper.DrawNestIndicator(1);
                    ImGui.DragInt("Icon Size##Threshold", ref TimelineThresholdIconSize);

                    ImGui.NewLine();
                    DrawHelper.DrawNestIndicator(1);
                    DrawHelper.DrawFontSelector("Font##Threshold Name", ref CooldownThresholdFontKey, ref CooldownThresholdFontId);
                    DrawHelper.DrawNestIndicator(1);
                    DrawHelper.DrawColorSelector("Cooldown Text Color##Threshold", ref CooldownTextThresholdColor);
                    DrawHelper.DrawNestIndicator(1);
                    DrawHelper.DrawColorSelector("Cooldown Text Outline Color##Threshold", ref CooldownTextOutlineThresholdColor);
                }
            }

            ImGui.EndChild();
        }
    }
}