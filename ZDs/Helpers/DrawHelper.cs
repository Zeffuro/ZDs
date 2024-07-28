using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using ZDs.Config;

namespace ZDs.Helpers
{
    internal static class DrawHelper
    {
        private static ZDsConfig Config => Plugin.Config;
        
        public static void DrawButton(
            string label,
            FontAwesomeIcon icon,
            Action clickAction,
            string? help = null,
            Vector2? size = null)
        {
            if (!string.IsNullOrEmpty(label))
            {
                ImGui.Text(label);
                ImGui.SameLine();
            }

            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(icon.ToIconString(), size ?? Vector2.Zero))
            {
                clickAction();
            }

            ImGui.PopFont();
            if (!string.IsNullOrEmpty(help) && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(help);
            }
        }
        
        public static void DrawIcon(uint iconId, Vector2 position, Vector2 size, float alpha, ImDrawListPtr drawList)
        {
            IDalamudTextureWrap? texture = TexturesHelper.GetTextureFromIconId(iconId);
            if (texture == null) return;

            uint color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha));
            drawList.AddImage(texture.ImGuiHandle, position, position + size, Vector2.Zero, Vector2.One, color);
        }
        
        public static void DrawIcon(uint iconId, Vector2 position, Vector2 size, bool drawBorder, float alpha, ImDrawListPtr drawList)
        {
            IDalamudTextureWrap? texture = TexturesHelper.GetTextureFromIconId(iconId);
            if (texture == null) { return; }

            uint color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha));
            drawList.AddImage(texture.ImGuiHandle, position, position + size, Vector2.Zero, Vector2.One, color);

            if (drawBorder)
            {
                drawList.AddRect(position, position + size, ImGui.ColorConvertFloat4ToU32(Config.CooldownConfig.BorderColor));
            }
        }
        
        public static void DrawIcon<T>(dynamic row, Vector2 position, Vector2 size, bool drawBorder, bool cropIcon) where T : ExcelRow
        {
            IDalamudTextureWrap texture = TexturesHelper.GetTexture<T>(row);
            if (texture == null) { return; }

            (Vector2 uv0, Vector2 uv1) = GetTexCoordinates(texture, size, cropIcon);

            ImGui.SetCursorPos(position);
            ImGui.Image(texture.ImGuiHandle, size, uv0, uv1);

            if (drawBorder)
            {
                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                drawList.AddRect(position, position + size, ImGui.ColorConvertFloat4ToU32(Config.CooldownConfig.BorderColor));
            }
        }
        
        // By fel1n3 https://github.com/DelvUI/DelvUI/pull/1235
        public static void DrawIconCooldown(Vector2 position, Vector2 size, double elapsed, double total, ImDrawListPtr drawList)
        {
            double completion = elapsed / total;
            int segments = (int)Math.Ceiling(completion * 4);

            Vector2 center = position + size / 2;
            
            //Define vertices for top, left, bottom, and right points relative to the center.
            Vector2[] vertices =
            [
                center with {Y = center.Y - size.Y}, // Top
                center with {X = center.X - size.X}, // Left
                center with {Y = center.Y + size.Y}, // Bottom
                center with {X = center.X + size.X}  // Right
            ];
            
            ImGui.PushClipRect(position, position + size, false);
            for (int i = 0; i < segments; i++)
            {
                Vector2 v2 = vertices[i % 4];
                Vector2 v3 = vertices[(i + 1) % 4];
                
                
                if (i == segments - 1)
                {   // If drawing the last segment, adjust the second vertex based on the cooldown.
                    float angle = 2 * MathF.PI * (1 - (float)completion);
                    float cos = MathF.Cos(angle);
                    float sin = MathF.Sin(angle);

                    v3 = center + Vector2.Multiply(new Vector2(sin,-cos), size);
                }

                drawList.AddTriangleFilled(center, v3, v2, 0xCC000000);
            }
            ImGui.PopClipRect();
        }
        
        public static (Vector2, Vector2) GetTexCoordinates(IDalamudTextureWrap texture, Vector2 size, bool cropIcon = true)
        {
            if (texture == null)
            {
                return (Vector2.Zero, Vector2.Zero);
            }

            // Status = 24x32, show from 2,7 until 22,26
            //show from 0,0 until 24,32 for uncropped status icon

            float uv0x = cropIcon ? 4f : 1f;
            float uv0y = cropIcon ? 14f : 1f;

            float uv1x = cropIcon ? 4f : 1f;
            float uv1y = cropIcon ? 12f : 1f;

            Vector2 uv0 = new(uv0x / texture.Width, uv0y / texture.Height);
            Vector2 uv1 = new(1f - uv1x / texture.Width, 1f - uv1y / texture.Height);

            return (uv0, uv1);
        }
        
        public static void DrawNotification(
            string message,
            NotificationType type = NotificationType.Info,
            uint durationInMs = 3000,
            string title = "ZDs")
        {
            Notification notification = new()
            {
                Title = title,
                Content = message,
                Type = type,
                InitialDuration = TimeSpan.FromMilliseconds(durationInMs),
                Minimized = false
            };

            Plugin.NotificationManager.AddNotification(notification);
        }
        
        public static void DrawOutlinedText(string text, Vector2 pos, uint color, uint outlineColor, ImDrawListPtr drawList, int thickness = 1)
        {
            // outline
            for (int i = 1; i < thickness + 1; i++)
            {
                drawList.AddText(new Vector2(pos.X - i, pos.Y + i), outlineColor, text);
                drawList.AddText(new Vector2(pos.X, pos.Y + i), outlineColor, text);
                drawList.AddText(new Vector2(pos.X + i, pos.Y + i), outlineColor, text);
                drawList.AddText(new Vector2(pos.X - i, pos.Y), outlineColor, text);
                drawList.AddText(new Vector2(pos.X + i, pos.Y), outlineColor, text);
                drawList.AddText(new Vector2(pos.X - i, pos.Y - i), outlineColor, text);
                drawList.AddText(new Vector2(pos.X, pos.Y - i), outlineColor, text);
                drawList.AddText(new Vector2(pos.X + i, pos.Y - i), outlineColor, text);
            }

            // text
            drawList.AddText(new Vector2(pos.X, pos.Y), color, text);
        }

        public static void DrawShadowText(string text, Vector2 pos, uint color, uint shadowColor, ImDrawListPtr drawList, int offset = 1, int thickness = 1)
        {
            // TODO: Add parameter to allow to choose a direction

            // Shadow
            for (int i = 0; i < thickness; i++)
            {
                drawList.AddText(new Vector2(pos.X + i + offset, pos.Y  + i + offset), shadowColor, text);
            }

            // Text
            drawList.AddText(new Vector2(pos.X, pos.Y), color, text);
        }
        
                public static (bool, bool) DrawConfirmationModal(string title, string message)
        {
            return DrawConfirmationModal(title, new string[] { message });
        }
                
        public static void DrawColorSelector(string label, ref ConfigColor color)
        {
            Vector4 vector = color.Vector;
            ImGui.ColorEdit4(label, ref vector, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar);
            color.Vector = vector;
        }

        public static (bool, bool) DrawConfirmationModal(string title, IEnumerable<string> textLines)
        {
            Config.GeneralConfig.ShowingModalWindow = true;

            bool didConfirm = false;
            bool didClose = false;

            ImGui.OpenPopup(title + " ##ZDs_Modal");

            Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool p_open = true; // i've no idea what this is used for

            if (ImGui.BeginPopupModal(title + " ##ZDs_Modal", ref p_open, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                float width = 300;
                float height = Math.Min((ImGui.CalcTextSize(" ").Y + 5) * textLines.Count(), 240);

                ImGui.BeginChild("confirmation_modal_message", new Vector2(width, height), false);
                foreach (string text in textLines)
                {
                    ImGui.Text(text);
                }
                ImGui.EndChild();

                ImGui.NewLine();

                if (ImGui.Button("OK", new Vector2(width / 2f - 5, 24)))
                {
                    ImGui.CloseCurrentPopup();
                    didConfirm = true;
                    didClose = true;
                }

                ImGui.SetItemDefaultFocus();
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(width / 2f - 5, 24)))
                {
                    ImGui.CloseCurrentPopup();
                    didClose = true;
                }

                ImGui.EndPopup();
            }
            // close button on nav
            else
            {
                didClose = true;
            }

            if (didClose)
            {
                Config.GeneralConfig.ShowingModalWindow = false;
            }

            return (didConfirm, didClose);
        }

        public static bool DrawErrorModal(string message)
        {
            Config.GeneralConfig.ShowingModalWindow = true;

            bool didClose = false;
            ImGui.OpenPopup("Error ##ZDs_Modal");

            Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool p_open = true; // i've no idea what this is used for
            if (ImGui.BeginPopupModal("Error ##ZDs_Modal", ref p_open, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text(message);
                ImGui.NewLine();

                var textSize = ImGui.CalcTextSize(message).X;

                if (ImGui.Button("OK", new Vector2(textSize, 24)))
                {
                    ImGui.CloseCurrentPopup();
                    didClose = true;
                }

                ImGui.EndPopup();
            }
            // close button on nav
            else
            {
                didClose = true;
            }

            if (didClose)
            {
                Config.GeneralConfig.ShowingModalWindow = false;
            }

            return didClose;
        }
        
        public static void DrawFontSelector(string label, ref string fontKey, ref int fontId)
        {
            string[] fontOptions = FontsManager.GetFontList();
            if (fontOptions.Length == 0)
            {
                return;
            }

            if (!FontsManager.ValidateFont(fontOptions, fontId, fontKey))
            {
                fontId = 0;
                for (int i = 0; i < fontOptions.Length; i++)
                {
                    if (fontKey.Equals(fontOptions[i]))
                    {
                        fontId = i;
                    }
                }
            }

            ImGui.Combo(label, ref fontId, fontOptions, fontOptions.Length);
            fontKey = fontOptions[fontId];
        }
        
        public static void DrawNestIndicator(int depth)
        {
            // This draws the L shaped symbols and padding to the left of config items collapsible under a checkbox.
            // Shift cursor to the right to pad for children with depth more than 1.
            // 26 is an arbitrary value I found to be around half the width of a checkbox
            Vector2 oldCursor = ImGui.GetCursorPos();
            Vector2 offset = new(26 * Math.Max(depth - 1, 0), 0);
            ImGui.SetCursorPos(oldCursor + offset);
            ImGui.TextColored(new Vector4(229f / 255f, 57f / 255f, 57f / 255f, 1f), "\u2002\u2514");
            ImGui.SetCursorPosY(oldCursor.Y);
            ImGui.SameLine();
        }

        public static void SetTooltip(string message)
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(message);
            }
        }
        
        public class WindowInteractionState
        {
            public bool Unlocked { get; private set; }
            public bool Hovered { get; private set; }
            public bool Dragging { get; private set; }
            public bool Locked { get; private set; }
            private bool _lastFrameWasDragging;
            private bool _lastFrameWasUnlocked;

            public void Update(Vector2 windowPos, Vector2 windowSize, bool locked)
            {
                Unlocked = !locked;
                Hovered = ImGui.IsMouseHoveringRect(windowPos, windowPos + windowSize);
                Dragging = _lastFrameWasDragging && ImGui.IsMouseDown(ImGuiMouseButton.Left);
                Locked = (Unlocked && !_lastFrameWasUnlocked || !Hovered) && !Dragging;

                _lastFrameWasDragging = Hovered || Dragging;
                _lastFrameWasUnlocked = Unlocked;
            }
        }
    }
}
