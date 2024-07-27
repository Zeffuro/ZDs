using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Numerics;
using Dalamud.Interface.Utility;
using ZDs.Helpers;

namespace ZDs.Windows
{
    public class SettingsWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;

        public SettingsWindow(string name) : base(name)
        {
            Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoScrollWithMouse;

            Size = new Vector2(700, 700);
        }

        public override void Draw()
        {
            if (!ImGui.BeginTabBar("##Timeline_Settings_TabBar"))
            {
                return;
            }

            ImGui.PushItemWidth(80 * _scale);

            // general
            if (ImGui.BeginTabItem("General##Timeline_General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }
            
            // abilities
            if (ImGui.BeginTabItem("Abilities##Timeline_Abilities"))
            {
                DrawAbilitiesTab();
                ImGui.EndTabItem();
            }

            // Cooldowns
            if (ImGui.BeginTabItem("Cooldowns##Timeline_Icons"))
            {
                DrawCooldownsTab();
                ImGui.EndTabItem();
            }

            // grid
            if (ImGui.BeginTabItem("Grid##Timeline_Grid"))
            {
                DrawGridTab();
                ImGui.EndTabItem();
            }
            
            // grid
            if (ImGui.BeginTabItem("Fonts##Timeline_Fonts"))
            {
                ImGui.EndTabItem();
            }
            
            // donate button
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(255f / 255f, 94f / 255f, 91f / 255f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(255f / 255f, 94f / 255f, 91f / 255f, .85f));

            ImGui.SetCursorPos(new Vector2(280 * _scale, 26 * _scale));
            if (ImGui.Button(FontAwesomeIcon.MugHot.ToIconString(), new Vector2(24 * _scale, 24 * _scale)))
            {
                Utils.OpenUrl("https://ko-fi.com/Zeffuro");
            }

            ImGui.PopStyleColor(2);
            ImGui.PopFont();
            DrawHelper.SetTooltip("Tip the developer at ko-fi.com");

            ImGui.EndTabBar();
        }

        public void DrawGeneralTab()
        {
            
        }

        public void DrawAbilitiesTab()
        {
            
        }

        public void DrawCooldownsTab()
        {
            
        }

        public void DrawGridTab()
        {
            
        }
        
        
    }
}
