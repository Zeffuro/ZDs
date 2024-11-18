using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using ZDs.Helpers;
using Newtonsoft.Json;
using Lumina.Excel;
using Action = Lumina.Excel.Sheets.Action;

namespace ZDs.Config
{
    public class AbilitiesConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }
        
        public string Name => "Abilities";
        
        public bool IgnoreAbilitiesBelow = true;
        public float IgnoreAbilitiesRecast = 2.50f;

        public bool FilterAbilities = false;
        public SortedList<string, uint> FilteredAbilities = new SortedList<string, uint>();
        public int FilterMode = 0;
        
        private string _input = "";

        private string? _errorMessage = null;
        private string? _importString = null;
        private bool _clearingList = false;
        
        private uint[] _specialAbilities =
        [
            3,      // Sprint
            6,      // Return
            37018   // Umbral Draw
        ];

        public IConfigPage GetDefault() => new GeneralConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            if (ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), border))
            {
                bool changed = false;
                ImGui.Checkbox("Ignore abilities with a recast time below: ", ref IgnoreAbilitiesBelow);

                if (IgnoreAbilitiesBelow)
                {
                    DrawHelper.DrawNestIndicator(1);
                    ImGui.DragFloat("##GCD", ref IgnoreAbilitiesRecast, 0.01f, 0, 1000);
                }
                ImGui.NewLine();
                
                var flags =
                    ImGuiTableFlags.RowBg |
                    ImGuiTableFlags.Borders |
                    ImGuiTableFlags.BordersOuter |
                    ImGuiTableFlags.BordersInner |
                    ImGuiTableFlags.ScrollY |
                    ImGuiTableFlags.SizingFixedSame;

                var sheet = Plugin.DataManager.GetExcelSheet<Action>();
                
                var iconSize = new Vector2(30, 30);
                var indexToRemove = -1;

                if (ImGui.BeginChild("Filter Abilities", new Vector2(0, 360), false, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    ImGui.Checkbox("Filter Abilities", ref FilterAbilities);

                    if (FilterAbilities)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.RadioButton("Blacklist", ref FilterMode, 0);
                        ImGui.SameLine();
                        ImGui.RadioButton("Whitelist", ref FilterMode, 1);

                        ImGui.Text("Type an ability name or ID");

                        ImGui.PushItemWidth(300);
                        if (ImGui.InputText("", ref _input, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            changed |= AddNewEntry(_input, sheet);
                            ImGui.SetKeyboardFocusHere(-1);
                        }

                        // add
                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), new Vector2(0, 0)))
                        {
                            changed |= AddNewEntry(_input, sheet);
                            ImGui.SetKeyboardFocusHere(-2);
                        }

                        // export
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 154);
                        if (ImGui.Button(FontAwesomeIcon.Upload.ToIconString(), new Vector2(0, 0)))
                        {
                            ImGui.SetClipboardText(ExportList());
                            ImGui.OpenPopup("export_succes_popup");
                        }
                        ImGui.PopFont();
                        DrawHelper.SetTooltip("Export List to Clipboard");

                        // export success popup
                        if (ImGui.BeginPopup("export_succes_popup"))
                        {
                            ImGui.Text("List exported to clipboard!");
                            ImGui.EndPopup();
                        }

                        // import
                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.Download.ToIconString(), new Vector2(0, 0)))
                        {
                            _importString = ImGui.GetClipboardText();
                        }
                        ImGui.PopFont();
                        DrawHelper.SetTooltip("Import List from Clipboard");

                        // clear
                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), new Vector2(0, 0)))
                        {
                            _clearingList = true;
                        }
                        ImGui.PopFont();
                        DrawHelper.SetTooltip("Clear List");

                        if (ImGui.BeginTable("table", 4, flags, new Vector2(583, FilteredAbilities.Count > 0 ? 200 : 40)))
                        {
                            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 0, 0);
                            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 0, 1);
                            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0, 2);
                            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 0, 3);

                            ImGui.TableSetupScrollFreeze(0, 1);
                            ImGui.TableHeadersRow();

                            for (int i = 0; i < FilteredAbilities.Count; i++)
                            {
                                var id = FilteredAbilities.Values[i];
                                var name = FilteredAbilities.Keys[i];

                                if (sheet?.GetRow(id) is not Action row)
                                {
                                    continue;
                                }

                                if (_input != "" && !name.ToUpper().Contains(_input.ToUpper()))
                                {
                                    continue;
                                }

                                ImGui.PushID(i.ToString());
                                ImGui.TableNextRow(ImGuiTableRowFlags.None, iconSize.Y);

                                // icon
                                if (ImGui.TableSetColumnIndex(0))
                                {
                                    DrawHelper.DrawIcon<Action>(row, ImGui.GetCursorPos(), iconSize, false, true);
                                }

                                // id
                                if (ImGui.TableSetColumnIndex(1))
                                {
                                    ImGui.Text(id.ToString());
                                }

                                // name
                                if (ImGui.TableSetColumnIndex(2))
                                {
                                    var displayName = row.Name.ExtractText();
                                    ImGui.Text(displayName);
                                }

                                // remove
                                if (ImGui.TableSetColumnIndex(3))
                                {
                                    ImGui.PushFont(UiBuilder.IconFont);
                                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
                                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
                                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), iconSize))
                                    {
                                        changed = true;
                                        indexToRemove = i;
                                    }
                                    ImGui.PopFont();
                                    ImGui.PopStyleColor(3);
                                }
                                ImGui.PopID();
                            }

                            ImGui.EndTable();
                        }
                        ImGui.Text("\u2002 \u2002");
                        ImGui.SameLine();
                    }
                }
                
                if (indexToRemove >= 0)
                {
                    FilteredAbilities.RemoveAt(indexToRemove);
                }
                
                ImGui.EndChild();
                // error message
                if (_errorMessage != null)
                {
                    if (DrawHelper.DrawErrorModal(_errorMessage))
                    {
                        _errorMessage = null;
                    }
                }

                // import confirmation
                if (_importString != null)
                {
                    string[] message = new string[] {
                        "All the elements in the list will be replaced.",
                        "Are you sure you want to import?"
                    };
                    var (didConfirm, didClose) = DrawHelper.DrawConfirmationModal("Import?", message);

                    if (didConfirm)
                    {
                        _errorMessage = ImportList(_importString);
                        changed = true;
                    }

                    if (didConfirm || didClose)
                    {
                        _importString = null;
                    }
                }

                // clear confirmation
                if (_clearingList)
                {
                    string message = "Are you sure you want to clear the list?";

                    var (didConfirm, didClose) = DrawHelper.DrawConfirmationModal("Clear List?", message);

                    if (didConfirm)
                    {
                        FilteredAbilities.Clear();
                        changed = true;
                    }

                    if (didConfirm || didClose)
                    {
                        _clearingList = false;
                    }
                }
            }

            ImGui.EndChild();
        }
        
        private string KeyName(Action status)
        {
            return $"{status.Name.ExtractText()}[{status.RowId}]";
        }
        
        public bool AddNewEntry(Action action)
        {
            if (!FilteredAbilities.ContainsKey(KeyName(action)))
            {
                FilteredAbilities.Add(KeyName(action), action.RowId);
                _input = "";

                return true;
            }

            return false;
        }

        private bool AddNewEntry(string input, ExcelSheet<Action> sheet)
        {
            if (input.Length > 0)
            {
                List<Action> actionToAdd = new List<Action>();

                // try id
                if (uint.TryParse(input, out uint uintValue))
                {
                    if (uintValue > 0)
                    {
                        if(sheet.GetRow(uintValue) is Action action && (action.IsPlayerAction || _specialAbilities.Contains(action.RowId)))
                        {
                            actionToAdd.Add(action);
                        }
                    }
                }

                // try name
                if (actionToAdd.Count == 0)
                {
                    var enumerator = sheet.GetEnumerator();

                    while (enumerator.MoveNext())
                    {
                        Action item = enumerator.Current;
                        if (item.Name.ToString().ToLower() == input.ToLower() && (item.IsPlayerAction || _specialAbilities.Contains(item.RowId)))
                        {
                            actionToAdd.Add(item);
                        }
                    }
                }

                bool added = false;
                foreach (Action action in actionToAdd)
                {
                    added |= AddNewEntry(action);
                }
                return added;
            }

            return false;
        }
        
        private string ExportList()
        {
            string exportString = "";

            for (int i = 0; i < FilteredAbilities.Keys.Count; i++)
            {
                exportString += FilteredAbilities.Keys[i] + "|";
                exportString += FilteredAbilities.Values[i] + "|";
            }

            return exportString;
        }

        private string? ImportList(string importString)
        {
            SortedList<string, uint> tmpList = new SortedList<string, uint>();

            try
            {
                string[] strings = importString.Trim().Split("|", StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < strings.Length; i += 2)
                {
                    if (i + 1 >= strings.Length)
                    {
                        break;
                    }

                    string key = strings[i];
                    uint value = uint.Parse(strings[i + 1]);

                    tmpList.Add(key, value);
                }
            }
            catch
            {
                return "Error importing list!";
            }

            FilteredAbilities = tmpList;
            return null;
        }
    }
}