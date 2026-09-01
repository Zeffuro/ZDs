using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;
using ZDs.Helpers;

namespace ZDs.Config
{
    public class ProfilesConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }

        [JsonIgnore]
        private string _newProfileName = string.Empty;

        [JsonIgnore]
        private bool _confirmImport;

        [JsonIgnore]
        private bool _confirmReset;

        public string Name => "Profiles";
        public IConfigPage GetDefault() => new ProfilesConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            using var child = ImRaii.Child("##ProfilesConfig", size, border);
            if (!child)
            {
                return;
            }

            ImGui.Text($"Active profile: {ProfileManager.ActiveProfile}");
            if (ImGui.Button("Save Active"))
            {
                NotifySave(ProfileManager.ActiveProfile, ProfileManager.SaveCurrent(ProfileManager.ActiveProfile));
            }

            ImGui.NewLine();
            ImGui.Text("Built-in presets");
            ImGui.TextDisabled("Applying a preset saves it as a new profile. You can switch back whenever you like.");
            using (var presetTable = ImRaii.Table(
                       "##BuiltInPresets",
                       3,
                       ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
            {
                if (presetTable)
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 130f);
                    ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 70f);
                    foreach (BuiltInPreset preset in BuiltInPresetCatalog.All)
                    {
                        using var id = ImRaii.PushId(preset.Id);
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(preset.Name);
                        ImGui.TableSetColumnIndex(1);
                        ImGui.TextWrapped(preset.Description);
                        ImGui.TableSetColumnIndex(2);
                        if (ImGui.Button("Apply"))
                        {
                            bool applied = ProfileManager.ApplyBuiltInPreset(preset.Id, createProfile: true);
                            DrawHelper.DrawNotification(
                                applied ? $"Applied preset '{preset.Name}'." : $"Could not apply preset '{preset.Name}'.",
                                applied ? NotificationType.Success : NotificationType.Error);
                        }
                    }
                }
            }

            ImGui.NewLine();
            ImGui.SetNextItemWidth(System.Math.Max(180, size.X - 120));
            ImGui.InputTextWithHint("##NewProfileName", "New profile name", ref _newProfileName, 64);
            ImGui.SameLine();
            if (ImGui.Button("Save As") && !string.IsNullOrWhiteSpace(_newProfileName))
            {
                string name = _newProfileName.Trim();
                bool saved = ProfileManager.SaveCurrent(name);
                NotifySave(name, saved);
                if (saved)
                {
                    _newProfileName = string.Empty;
                }
            }

            ImGui.NewLine();
            ImGui.Text("Saved profiles");

            ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.NoSavedSettings;
            using (var table = ImRaii.Table("##ProfilesTable", 2, flags, new Vector2(size.X - padX * 2, 0)))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 190);
                    ImGui.TableHeadersRow();

                    foreach (string name in ProfileManager.ProfileNames.ToArray())
                    {
                        bool active = string.Equals(name, ProfileManager.ActiveProfile, System.StringComparison.OrdinalIgnoreCase);
                        using var id = ImRaii.PushId(name);
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(active ? $"{name} (active)" : name);
                        ImGui.TableSetColumnIndex(1);

                        using (ImRaii.Disabled(active))
                        {
                            if (ImGui.Button("Load"))
                            {
                                bool switched = ProfileManager.Switch(name);
                                DrawHelper.DrawNotification(
                                    switched ? $"Switched to profile '{name}'." : $"Could not load profile '{name}'.",
                                    switched ? NotificationType.Success : NotificationType.Error);
                            }
                        }
                        ImGui.SameLine();

                        if (ImGui.Button("Overwrite"))
                        {
                            NotifySave(name, ProfileManager.SaveCurrent(name, active));
                        }
                        ImGui.SameLine();

                        using (ImRaii.Disabled(active))
                        {
                            if (ImGui.Button("Delete") && ProfileManager.Delete(name))
                            {
                                DrawHelper.DrawNotification($"Deleted profile '{name}'.");
                            }
                        }
                    }
                }
            }

            ImGui.NewLine();
            ImGui.Separator();
            ImGui.TextUnformatted("Transfer");
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(FontAwesomeIcon.Upload.ToIconString()))
                {
                    ConfigHelpers.ExportConfig();
                }
            }
            DrawHelper.SetTooltip("Export active profile to clipboard.");

            ImGui.SameLine();
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(FontAwesomeIcon.Download.ToIconString()))
                {
                    _confirmImport = true;
                }
            }
            DrawHelper.SetTooltip("Import clipboard as a new profile.");

            ImGui.SameLine();
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString()))
                {
                    _confirmReset = true;
                }
            }
            DrawHelper.SetTooltip("Delete all ZDs settings and profiles.");

            if (_confirmImport)
            {
                var (confirmed, closed) = DrawHelper.DrawConfirmationModal(
                    "Import Profile?",
                    "Create a new profile from the export string on the clipboard?");
                if (confirmed)
                {
                    bool imported = ProfileManager.Import(ImGui.GetClipboardText());
                    DrawHelper.DrawNotification(
                        imported ? $"Imported profile '{ProfileManager.ActiveProfile}'." : "That clipboard text is not a valid ZDs profile.",
                        imported ? NotificationType.Success : NotificationType.Error);
                }

                if (confirmed || closed)
                {
                    _confirmImport = false;
                }
            }

            if (_confirmReset)
            {
                var (confirmed, closed) = DrawHelper.DrawConfirmationModal(
                    "Reset ZDs?",
                    [
                        "This deletes all settings and profiles, like Delete Plugin Config in the installer.",
                        "Fonts copied into the ZDs config folder are deleted too."
                    ]);
                if (confirmed)
                {
                    ConfigHelpers.ResetConfig();
                }

                if (confirmed || closed)
                {
                    _confirmReset = false;
                }
            }
        }

        private static void NotifySave(string name, bool saved)
        {
            DrawHelper.DrawNotification(
                saved ? $"Saved profile '{name}'." : $"Could not save profile '{name}'.",
                saved ? NotificationType.Success : NotificationType.Error);
        }
    }
}
