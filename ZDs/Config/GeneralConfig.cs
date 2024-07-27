using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ZDs.Helpers;
using Newtonsoft.Json;

namespace ZDs.Config
{
    public class GeneralConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }
        
        public string Name => "General";
        
        [JsonIgnore]
        public bool ShowingModalWindow = false;
        
        public bool ShowTimeline = true;
        public bool TimelineLocked = false;
        public bool ShowTimelineOnlyInDuty = false;
        public bool ShowTimelineOnlyInCombat = false;
        public int TimelineTime = 120; // seconds
        public float TimelineCompression = 0.3f;

        public ConfigColor TimelineLockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0f);
        public ConfigColor TimelineUnlockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0.75f);
        
        public bool ReverseIconDrawOrder = false;

        public bool TimelineBorder = false;
        public float TimelineBorderThickness = 1f;

        public IConfigPage GetDefault() => new GeneralConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            if (ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), border))
            {
                ImGui.Checkbox("Enabled", ref ShowTimeline);

                if (ShowTimeline)
                {

                    ImGui.DragInt("Time (seconds)", ref TimelineTime, 0.1f, 1, 300);
                    DrawHelper.SetTooltip("This is how far in the past the timeline will go.");

                    ImGui.DragFloat("Time compression", ref TimelineCompression, 0.1f, 0, 10);
                    DrawHelper.SetTooltip("This controls how much the icons speed up getting off cooldown, values above 1 will instead slow them down.");
                    
                    ImGui.NewLine();
                    ImGui.Checkbox("Reverse icon draw order", ref ReverseIconDrawOrder);
                    DrawHelper.SetTooltip("Enabling this will make the cooldowns that were just added to draw over the ones before.");
                    
                    ImGui.NewLine();
                    ImGui.Checkbox("Border", ref TimelineBorder);

                    if (TimelineBorder)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.DragFloat("Border Thickness", ref TimelineBorderThickness, 1f, 1, 10);
                    }

                    ImGui.NewLine();
                    ImGui.Checkbox("Locked", ref TimelineLocked);
                    DrawHelper.DrawColorSelector("Locked Color", ref TimelineLockedBackgroundColor);
                    DrawHelper.DrawColorSelector("Unlocked Color", ref TimelineUnlockedBackgroundColor);

                    ImGui.NewLine();

                    ImGui.Checkbox("Show Only In Duty", ref ShowTimelineOnlyInDuty);
                    ImGui.Checkbox("Show Only In Combat", ref ShowTimelineOnlyInCombat);
                }
            }

            ImGui.EndChild();
        }
    }
}