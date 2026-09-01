using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ZDs.Helpers;
using Newtonsoft.Json;

namespace ZDs.Config
{
    public enum Orientation
    {
        RightToLeft = 0,
        LeftToRight = 1,
        TopToBottom = 2,
        BottomToTop = 3
    }
    
    public class GeneralConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }
        
        public string Name => "General";
        
        public Vector2 Position = new Vector2(ImGui.GetMainViewport().Size.X / 2, ImGui.GetMainViewport().Size.Y / 2);
        public Vector2 Size = new Vector2(400, 100);
        
        public Orientation TimelineOrientation = Orientation.RightToLeft;
        
        [JsonIgnore]
        public bool ShowingModalWindow = false;
        
        public bool ShowTimeline = true;
        public bool TimelineLocked = false;
        public bool ShowTimelineOnlyInDuty = false;
        public bool ShowTimelineOnlyInCombat = false;
        public bool ShouldClip = true;
        public int TimelineTime = 120; // seconds
        public float TimelineCompression = 0.3f;

        public ConfigColor TimelineLockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0f);
        public ConfigColor TimelineUnlockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0.75f);
        
        public bool ReverseIconDrawOrder = false;

        public bool TimelineBorder = false;
        public float TimelineBorderThickness = 1f;

        public bool Preview = false;
        
        
        public IConfigPage GetDefault() => new GeneralConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            using var child = ImRaii.Child($"##{this.Name}", new Vector2(size.X, size.Y), border);
            if (!child)
            {
                return;
            }

                ImGui.Checkbox("Enabled", ref ShowTimeline);

                if (ShowTimeline)
                {
                    ImGui.Checkbox("Preview", ref Preview);
                    ImGui.NewLine();
                    
                    Vector2 screenSize = ImGui.GetMainViewport().Size;
                    ImGui.DragFloat2("Position", ref Position, 1, -screenSize.X / 2, screenSize.X);
                    
                    ImGui.DragFloat2("Size", ref Size, 1, 0, screenSize.X);
                    
                    ImGui.NewLine();
                    ImGui.Checkbox("Border", ref TimelineBorder);

                    if (TimelineBorder)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.DragFloat("Border Thickness", ref TimelineBorderThickness, 1f, 1, 10);
                    }

                    ImGui.NewLine();
                    ImGui.Checkbox("Locked", ref TimelineLocked);
                    DrawHelper.SetTooltip("When enabled, the timeline window cannot be moved or resized. The background color will change to indicate its locked state.");
                    DrawHelper.DrawColorSelector("Locked Color", ref TimelineLockedBackgroundColor);
                    DrawHelper.SetTooltip("The background color of the timeline window when it is locked.");
                    DrawHelper.DrawColorSelector("Unlocked Color", ref TimelineUnlockedBackgroundColor);
                    DrawHelper.SetTooltip("The background color of the timeline window when it is unlocked (movable and resizable).");

                    ImGui.NewLine();
                    ImGui.Checkbox("Show Only In Duty", ref ShowTimelineOnlyInDuty);
                    ImGui.Checkbox("Show Only In Combat", ref ShowTimelineOnlyInCombat);
                    
                    ImGui.NewLine();
                    ImGui.Checkbox("Hide When Covered by Game UI Elements", ref ShouldClip);
                    
                }
        }

        
    }
}
