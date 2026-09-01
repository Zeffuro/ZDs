using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
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
        public AbilityPriorityConfig PriorityRules = new();
        
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

        public IConfigPage GetDefault() => new AbilitiesConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            using var child = ImRaii.Child($"##{this.Name}", new Vector2(size.X, size.Y), border);
            if (!child)
            {
                return;
            }

                bool changed = false;
                ImGui.Checkbox("Ignore short cooldowns", ref IgnoreAbilitiesBelow);
                DrawHelper.SetTooltip("Hide abilities at or below this recast time.");

                if (IgnoreAbilitiesBelow)
                {
                    DrawHelper.DrawNestIndicator(1);
                    ImGui.DragFloat("##GCD", ref IgnoreAbilitiesRecast, 0.01f, 0, 1000);
                }

                var flags =
                    ImGuiTableFlags.RowBg |
                    ImGuiTableFlags.Borders |
                    ImGuiTableFlags.BordersOuter |
                    ImGuiTableFlags.BordersInner |
                    ImGuiTableFlags.ScrollY |
                    ImGuiTableFlags.SizingFixedSame;

                var sheet = IDataManager.Get().GetExcelSheet<Action>();
                
                var iconSize = new Vector2(30, 30);
                var indexToRemove = -1;

                float filterHeight = FilterAbilities ? 360f : ImGui.GetFrameHeightWithSpacing();
                using (var filterChild = ImRaii.Child("Filter Abilities", new Vector2(0, filterHeight), false, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    if (filterChild)
                    {
                    ImGui.Checkbox("Filter Abilities", ref FilterAbilities);
                    DrawHelper.SetTooltip("Limit which abilities appear.");

                    if (FilterAbilities)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.RadioButton("Hide listed", ref FilterMode, 0);
                        ImGui.SameLine();
                        ImGui.RadioButton("Only show listed", ref FilterMode, 1);

                        ImGui.Text("Type an ability name or ID");

                        using var itemWidth = ImRaii.ItemWidth(300);
                        if (ImGui.InputText("", ref _input, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            changed |= AddNewEntry(_input, sheet);
                            ImGui.SetKeyboardFocusHere(-1);
                        }
                        DrawHelper.SetTooltip("Enter an exact ability name or ID.");

                        // add
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
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
                        }
                        DrawHelper.SetTooltip("Export List to Clipboard");

                        // export success popup
                        using (var popup = ImRaii.Popup("export_succes_popup"))
                        {
                            if (popup)
                            {
                                ImGui.Text("List exported to clipboard!");
                            }
                        }

                        // import
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                        if (ImGui.Button(FontAwesomeIcon.Download.ToIconString(), new Vector2(0, 0)))
                        {
                            _importString = ImGui.GetClipboardText();
                        }
                        }
                        DrawHelper.SetTooltip("Import List from Clipboard");

                        // clear
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                        if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), new Vector2(0, 0)))
                        {
                            _clearingList = true;
                        }
                        }
                        DrawHelper.SetTooltip("Clear List");

                        using var table = ImRaii.Table("table", 4, flags, new Vector2(583, FilteredAbilities.Count > 0 ? 200 : 40));
                        if (table)
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

                                using var idScope = ImRaii.PushId(i);
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
                                    using var iconFont = ImRaii.PushFont(UiBuilder.IconFont);
                                    using var colors = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero)
                                        .Push(ImGuiCol.ButtonActive, Vector4.Zero)
                                        .Push(ImGuiCol.ButtonHovered, Vector4.Zero);
                                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), iconSize))
                                    {
                                        changed = true;
                                        indexToRemove = i;
                                    }
                                }
                            }
                        }
                        ImGui.Text("\u2002 \u2002");
                        ImGui.SameLine();
                    }
                    }
                }
                
                if (indexToRemove >= 0)
                {
                    FilteredAbilities.RemoveAt(indexToRemove);
                }

                if (sheet != null)
                {
                    PriorityRules.Draw(sheet);
                }
                
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
