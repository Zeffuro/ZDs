using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Interface;
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
        public bool ShouldClip = true;
        public int TimelineTime = 120; // seconds
        public float TimelineCompression = 0.3f;

        public ConfigColor TimelineLockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0f);
        public ConfigColor TimelineUnlockedBackgroundColor = new ConfigColor(0f, 0f, 0f, 0.75f);
        
        public bool ReverseIconDrawOrder = false;

        public bool TimelineBorder = false;
        public float TimelineBorderThickness = 1f;
        
        
        private bool _clickedImport = false;
        private bool _clickedReset = false;

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
                    
                    ImGui.NewLine();
                    ImGui.Checkbox("Hide When Covered by Game UI Elements", ref ShouldClip);
                    
                    ImGui.NewLine();
                    
                    // Export
                    ImGui.PushFont(UiBuilder.IconFont);
                    
                    if (ImGui.Button(FontAwesomeIcon.Upload.ToIconString(), new Vector2(0, 0)))
                    {
                        ConfigHelpers.ExportConfig();
                    }
                    ImGui.PopFont();
                    DrawHelper.SetTooltip("Export Config to Clipboard");

                    // Import
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(FontAwesomeIcon.Download.ToIconString(), new Vector2(0, 0)))
                    {
                        _clickedImport = true;
                    }
                    ImGui.PopFont();
                    DrawHelper.SetTooltip("Import Config from Clipboard");

                    // Reset
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), new Vector2(0, 0)))
                    {
                        _clickedReset = true;
                        string[] message = new string[] {
                            "Are you sure you want to reset your configuration?",
                            "Everything will be reset to the default settings!"
                        };
                        var (didConfirm, _) = DrawHelper.DrawConfirmationModal("Reset?", message);

                        if (didConfirm)
                        {
                            ConfigHelpers.ResetConfig();
                        }
                    }
                    ImGui.PopFont();
                    DrawHelper.SetTooltip("Reset to Defaults");
                }
            }
            
            if (_clickedImport)
            {
                
                string[] message = new string[] {
                    "Are you sure you want to import this configuration?"
                };
                var (didConfirm, didClose) = DrawHelper.DrawConfirmationModal("Import?", message);
                        
                if (didConfirm)
                {
                    ConfigHelpers.ImportConfig();
                    _clickedImport = false;
                    Plugin.ToggleSettingsWindow();
                }

                if (didClose)
                {
                    _clickedImport = false;
                }
            }
            
            if (_clickedReset)
            {
                
                string[] message = new string[] {
                    "Are you sure you want to reset your configuration?",
                    "Everything will be reset to the default settings!"
                };
                var (didConfirm, didClose) = DrawHelper.DrawConfirmationModal("Reset?", message);

                if (didConfirm)
                {
                    ConfigHelpers.ResetConfig();
                    _clickedReset = false;
                    Plugin.ToggleSettingsWindow();
                }

                if (didClose)
                {
                    _clickedReset = false;
                }
            }

            ImGui.EndChild();
        }

        
    }
}