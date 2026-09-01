using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ZDs.Helpers;
using Newtonsoft.Json;

namespace ZDs.Config
{
    public enum CooldownFinishAnimation
    {
        None = 0,
        Fade = 1,
        Pop = 2,
        Pulse = 3,
        Ants = 4,
        Breathe = 5,
    }

    public class CooldownConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }

        public string Name => "Cooldown";

        public int TimelineIconSize = 40;
        public bool DrawIconBorder = true;
        public Vector4 BorderColor = new Vector4(0f, 0f, 0f, 1f);

        public bool DrawIconCooldown = true;
        public bool ShowChargePips;
        public bool ShowNextChargeGhost;

        public CooldownFinishAnimation FinishAnimation = CooldownFinishAnimation.None;
        public float FinishAnimationDuration = 0.8f;
        public float FinishAnimationSpeed = 1f;
        public ConfigColor FinishAnimationColor = new ConfigColor(1f, 0.78f, 0.12f, 1f);
        public int FinishAnimationThickness = 2;
        public float FinishAnimationDashLength = 5f;

        public bool TimelineThresholdEnabled = true;
        public bool TimelineThresholdAnts = false;
        public int TimelineThresholdTime = 5;
        public int TimelineThresholdIconSize = 50;
        public float TimelineThresholdAntSpeed = 1f;
        public ConfigColor TimelineThresholdAntColor = new ConfigColor(1f, 0.78f, 0.12f, 1f);
        public int TimelineThresholdAntThickness = 2;
        public float TimelineThresholdAntDashLength = 5f;

        public LabelTextConfig CooldownThresholdText = new(
            FontsManager.DefaultBigFontSize,
            new ConfigColor(1f, 0f, 0f, 1f),
            new ConfigColor(0f, 0f, 0f, 1f));

        [JsonProperty("CooldownThresholdFontKey")]
        private string? LegacyCooldownThresholdFontKey
        {
            set => CooldownThresholdText.MigrateFont(value);
        }

        public bool CooldownIconOffsetEnabled = false;
        public bool CooldownIconOffsetInverted = false;
        public int CooldownIconOffset = 5;

        [JsonProperty("CooldownTextThresholdColor")]
        private ConfigColor LegacyCooldownTextThresholdColor
        {
            set => CooldownThresholdText.Color = value;
        }

        [JsonProperty("CooldownTextOutlineThresholdColor")]
        private ConfigColor LegacyCooldownTextOutlineThresholdColor
        {
            set => CooldownThresholdText.EffectColor = value;
        }

        public LabelTextConfig CooldownText = new(
            FontsManager.DefaultMediumFontSize,
            new ConfigColor(1f, 1f, 1f, 1f),
            new ConfigColor(0f, 0f, 0f, 1f));

        [JsonProperty("CooldownFontKey")]
        private string? LegacyCooldownFontKey
        {
            set => CooldownText.MigrateFont(value);
        }

        [JsonProperty("CooldownTextColor")]
        private ConfigColor LegacyCooldownTextColor
        {
            set => CooldownText.Color = value;
        }

        [JsonProperty("CooldownTextOutlineColor")]
        private ConfigColor LegacyCooldownTextOutlineColor
        {
            set => CooldownText.EffectColor = value;
        }

        public int RoundingMode = 2;
        public bool ShowCooldownAsMinutes = true;

        public IConfigPage GetDefault() => new CooldownConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            using var child = ImRaii.Child($"##{this.Name}", new Vector2(size.X, size.Y), border);
            if (!child)
            {
                return;
            }

                ImGui.Checkbox("Draw Border", ref DrawIconBorder);
                ImGui.ColorEdit4("Border Color", ref BorderColor, ImGuiColorEditFlags.NoInputs);
                ImGui.NewLine();
                ImGui.DragInt("Icon Size", ref TimelineIconSize);

                ImGui.NewLine();

                DrawHelper.DrawLabelTextConfig("Cooldown", CooldownText, colorLabel: "Cooldown Text Color");

                ImGui.NewLine();

                ImGui.Checkbox("Draw Cooldown Swipe", ref DrawIconCooldown);

                ImGui.Checkbox("Show Charge Pips", ref ShowChargePips);
                DrawHelper.SetTooltip("Show available charges on the icon.");
                if (ShowChargePips)
                {
                    DrawHelper.DrawNestIndicator(1);
                    ImGui.Checkbox("Next Charge Preview", ref ShowNextChargeGhost);
                    DrawHelper.SetTooltip("Show where the following charge will recover.");
                }

                ImGui.NewLine();

                int finishAnimation = (int)FinishAnimation;
                if (ImGui.Combo("At Zero", ref finishAnimation, ["Disappear (current behavior)", "Fade", "Pop", "Pulse", "Ants", "Breathe"], 6))
                {
                    FinishAnimation = (CooldownFinishAnimation)finishAnimation;
                }
                DrawHelper.SetTooltip("Choose what happens when an icon reaches zero.");

                if (FinishAnimation != CooldownFinishAnimation.None)
                {
                    DrawHelper.DrawNestIndicator(1);
                    ImGui.DragFloat("Duration##FinishAnimation", ref FinishAnimationDuration, 0.05f, 0.1f, 3f, "%.2f s");

                    if (FinishAnimation is CooldownFinishAnimation.Pulse or CooldownFinishAnimation.Ants or CooldownFinishAnimation.Breathe)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.DragFloat("Speed##FinishAnimation", ref FinishAnimationSpeed, 0.05f, -4f, 4f, "%.2fx");
                        DrawHelper.SetTooltip("Negative speed reverses Ants.");
                    }

                    if (FinishAnimation is CooldownFinishAnimation.Pop or CooldownFinishAnimation.Pulse or CooldownFinishAnimation.Ants or CooldownFinishAnimation.Breathe)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        DrawHelper.DrawColorSelector("Accent Color##FinishAnimation", ref FinishAnimationColor);
                    }

                    if (FinishAnimation == CooldownFinishAnimation.Ants)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.DragInt("Thickness##FinishAnimation", ref FinishAnimationThickness, 1, 1, 8);
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.DragFloat("Dash Length##FinishAnimation", ref FinishAnimationDashLength, 0.25f, 2f, 20f, "%.1f px");
                    }
                }

                ImGui.NewLine();

                ImGui.Checkbox("Duration in minutes", ref ShowCooldownAsMinutes);

                ImGui.Combo("Rounding Mode", ref RoundingMode, ["Truncate", "Floor", "Ceil", "Round"], 4);
                DrawHelper.SetTooltip("Controls how cooldown timers are rounded for display. For example, 'Truncate' removes decimals, while 'Round' rounds to the nearest whole number.");

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
                    ImGui.Checkbox("Ants##Threshold", ref TimelineThresholdAnts);
                    DrawHelper.SetTooltip("Show Ants inside the threshold.");

                    if (TimelineThresholdAnts)
                    {
                        DrawHelper.DrawNestIndicator(2);
                        ImGui.DragFloat("Ant Speed##Threshold", ref TimelineThresholdAntSpeed, 0.05f, -4f, 4f, "%.2fx");
                        DrawHelper.DrawNestIndicator(2);
                        DrawHelper.DrawColorSelector("Ant Color##Threshold", ref TimelineThresholdAntColor);
                        DrawHelper.DrawNestIndicator(2);
                        ImGui.DragInt("Ant Thickness##Threshold", ref TimelineThresholdAntThickness, 1, 1, 8);
                        DrawHelper.DrawNestIndicator(2);
                        ImGui.DragFloat("Ant Dash Length##Threshold", ref TimelineThresholdAntDashLength, 0.25f, 2f, 20f, "%.1f px");
                    }

                    ImGui.NewLine();
                    DrawHelper.DrawLabelTextConfig("CooldownThreshold", CooldownThresholdText, 1, "Cooldown Text Color");
                }
        }
    }
}
