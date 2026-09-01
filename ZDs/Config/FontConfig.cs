using System.Collections.Generic;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using ZDs.Helpers;

namespace ZDs.Config
{
    public class FontConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }
        
        public string Name => "Fonts";

        [JsonIgnore] private static readonly string? _fontPath = FontsManager.GetUserFontPath();
        [JsonIgnore] private int _selectedFont = 0;
        [JsonIgnore] private int _selectedGameFont = 0;
        [JsonIgnore] private string[] _fontPaths = FontsManager.GetFontPaths(FontsManager.GetUserFontPath());
        [JsonIgnore] private readonly string[] _gameFontFamilies = Enum.GetValues<GameFontFamily>()
            .Where(family => family != GameFontFamily.Undefined)
            .Select(family => family.ToString())
            .ToArray();
        [JsonIgnore] private bool _chinese = false;
        [JsonIgnore] private bool _korean = false;

        public Dictionary<string, FontData> Fonts { get; set; }

        public FontConfig()
        {
            RefreshFontList();
            this.Fonts = [];

            foreach (FontData font in FontsManager.GetDefaultFontData())
            {
                this.Fonts.Add(FontsManager.GetFontKey(font), font);
            }
        }

        public IConfigPage GetDefault() => new FontConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            if (_fontPaths.Length == 0)
            {
                RefreshFontList();
            }

            using var child = ImRaii.Child("##FontConfig", new Vector2(size.X, size.Y), border);
            if (!child)
            {
                return;
            }

                if (_fontPath is not null)
                {
                    float cursorY = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosY(cursorY + 2f);
                    ImGui.Text("Copy Font Folder Path to Clipboard: ");
                    DrawHelper.SetTooltip("Put .ttf or .otf files here, then reload the list.");
                    ImGui.SameLine();

                    Vector2 buttonSize = new(40, 0);
                    ImGui.SetCursorPosY(cursorY);
                    DrawHelper.DrawButton(string.Empty, FontAwesomeIcon.Copy, () => ImGui.SetClipboardText(_fontPath), null, buttonSize);

                    string[] fontNames = _fontPaths.Select(x => FontsManager.GetFontName(_fontPath, x)).ToArray();
                    ImGui.Combo("Font", ref _selectedFont, fontNames, fontNames.Length);
                    ImGui.SameLine();
                    DrawHelper.DrawButton(string.Empty, FontAwesomeIcon.Sync, () => RefreshFontList(), "Reload Font List", buttonSize);

                    DrawHelper.DrawButton(string.Empty, FontAwesomeIcon.Plus, () => AddFont(_selectedFont), "Add Font Family", buttonSize);

                    ImGui.Checkbox("Support Chinese/Japanese", ref _chinese);
                    ImGui.SameLine();
                    ImGui.Checkbox("Support Korean", ref _korean);

                    ImGui.NewLine();
                    ImGui.Text("Add FFXIV Game Font");
                    ImGui.Combo("Family##GameFont", ref _selectedGameFont, _gameFontFamilies, _gameFontFamilies.Length);
                    ImGui.SameLine();
                    DrawHelper.DrawButton(string.Empty, FontAwesomeIcon.Plus, () => AddGameFont(_selectedGameFont), "Add Game Font Family", buttonSize);
                    DrawHelper.SetTooltip("Add a font included with FFXIV.");

                    //DrawHelpers.DrawSpacing(1);
                    ImGui.Text("Font List");
                    DrawHelper.SetTooltip("Added fonts appear in other settings.");

                    ImGuiTableFlags tableFlags =
                        ImGuiTableFlags.RowBg |
                        ImGuiTableFlags.Borders |
                        ImGuiTableFlags.BordersOuter |
                        ImGuiTableFlags.BordersInner |
                        ImGuiTableFlags.ScrollY |
                        ImGuiTableFlags.NoSavedSettings;

                    using var table = ImRaii.Table("##Font_Table", 5, tableFlags, new Vector2(size.X - padX * 2, size.Y - ImGui.GetCursorPosY() - padY * 2));
                    if (table)
                    {
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0, 0);
                        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 70, 1);
                        ImGui.TableSetupColumn("CN/JP", ImGuiTableColumnFlags.WidthFixed, 40, 2);
                        ImGui.TableSetupColumn("KR", ImGuiTableColumnFlags.WidthFixed, 40, 3);
                        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 45, 4);

                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < this.Fonts.Keys.Count; i++)
                        {
                            using var id = ImRaii.PushId(i);
                            ImGui.TableNextRow(ImGuiTableRowFlags.None, 28);

                            string key = this.Fonts.Keys.ElementAt(i);
                            FontData font = this.Fonts[key];

                            if (ImGui.TableSetColumnIndex(0))
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3f);
                                ImGui.Text(key);
                            }

                            if (ImGui.TableSetColumnIndex(1))
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3f);
                                ImGui.Text(font.Source == FontSourceKind.Game ? "FFXIV" : "File");
                            }

                            if (ImGui.TableSetColumnIndex(2))
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3f);
                                ImGui.Text(font.Chinese ? "Yes" : "No");
                            }

                            if (ImGui.TableSetColumnIndex(3))
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3f);
                                ImGui.Text(font.Korean ? "Yes" : "No");
                            }

                            if (ImGui.TableSetColumnIndex(4))
                            {
                                if (!key.Equals(FontsManager.DefaultFontFamily, StringComparison.Ordinal))
                                {
                                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
                                    DrawHelper.DrawButton(string.Empty, FontAwesomeIcon.Trash, () => RemoveFont(key), "Remove Font", new Vector2(45, 0));
                                }
                            }
                        }

                    }
                }
        }

        public void RefreshFontList()
        {
            _fontPaths = FontsManager.GetFontPaths(FontsManager.GetUserFontPath());
        }

        public void NormalizeFonts()
        {
            Dictionary<string, FontData> normalized = [];
            foreach (FontData font in Fonts.Values.Concat(FontsManager.GetDefaultFontData()))
            {
                string family = FontsManager.GetFontFamilyKey(font);
                normalized.TryAdd(family, font);
            }

            Fonts = normalized;
        }

        private void AddFont(int fontIndex)
        {
            if (fontIndex < 0 || fontIndex >= _fontPaths.Length)
            {
                return;
            }

            FontData newFont = new(FontsManager.GetFontName(_fontPath, _fontPaths[fontIndex]), _fontPaths[fontIndex], FontsManager.DefaultMediumFontSize, _chinese, _korean);
            string key = FontsManager.GetFontKey(newFont);

            if (this.Fonts.TryAdd(key, newFont))
            {
                FontsManager manager = Singletons.Get<FontsManager>();
                manager.UpdateFonts(this.Fonts.Values);
                if (!manager.HasFont(key))
                {
                    this.Fonts.Remove(key);
                    DrawHelper.DrawNotification($"Could not load font '{key}'. Check the plugin log for details.", NotificationType.Error);
                    return;
                }

                ConfigHelpers.SaveConfig();
                DrawHelper.DrawNotification($"Added font '{key}'.");
            }
            else
            {
                DrawHelper.DrawNotification($"Font '{key}' is already in the list.", NotificationType.Warning);
            }
        }

        private void AddGameFont(int familyIndex)
        {
            if (familyIndex < 0 || familyIndex >= _gameFontFamilies.Length)
            {
                return;
            }

            string family = _gameFontFamilies[familyIndex];
            FontData newFont = new(family, string.Empty, FontsManager.DefaultMediumFontSize, false, false, FontSourceKind.Game, family);
            string key = FontsManager.GetFontKey(newFont);

            if (Fonts.TryAdd(key, newFont))
            {
                FontsManager manager = Singletons.Get<FontsManager>();
                manager.UpdateFonts(Fonts.Values);
                if (!manager.HasFont(key))
                {
                    Fonts.Remove(key);
                    DrawHelper.DrawNotification($"Could not load FFXIV font '{key}'. Check the plugin log for details.", NotificationType.Error);
                    return;
                }

                ConfigHelpers.SaveConfig();
                DrawHelper.DrawNotification($"Added FFXIV font '{key}'.");
            }
            else
            {
                DrawHelper.DrawNotification($"Font '{key}' is already in the list.", NotificationType.Warning);
            }
        }

        private void RemoveFont(string key)
        {
            this.Fonts.Remove(key);
            Singletons.Get<FontsManager>().UpdateFonts(this.Fonts.Values);
            ConfigHelpers.SaveConfig();
        }
    }
}
