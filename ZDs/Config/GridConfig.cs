using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Interface;
using ImGuiNET;
using ZDs.Helpers;
using Newtonsoft.Json;

namespace ZDs.Config
{
    public class GridConfig : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }
        
        public string Name => "Grid";
        
        public bool ShowGrid = true;
        public bool ShowGridCenterLine = true;
        public bool GridDrawSegments = true;
        public bool GridDrawSegmentLines = true;
        public bool GridShowSecondsText = true;
        public float GridLineWidth = 1;
        public float GridLineHeight = 15;
        public ConfigColor GridLineColor = new ConfigColor(1f, 1f, 1f, 1f);
        public List<float> GridSegments = new List<float> { 10f, 30f, 60f, 120f };
        
        public string GridFontKey = FontsManager.DefaultSmallFontKey;
        public int GridFontId = 0;
        
        public ConfigColor GridSegmentLabelColor = new ConfigColor(1f, 1f, 1f, 1f);
        public ConfigColor GridSegmentLabelOutlineColor = new ConfigColor(0f, 0f, 0f, 1f);
        
        public float GridSegmentLabelOffset = 5f;
        public bool GridSegmentLabelAnchorBottom = true;
        
        private int _segmentInput;

        public IConfigPage GetDefault() => new GeneralConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            if (ImGui.BeginChild($"##{Name}", new Vector2(size.X, size.Y), border))
            {
                ImGui.Checkbox("Enabled", ref ShowGrid);

                if (ShowGrid) { 
                
                    ImGui.NewLine();

                    ImGui.Checkbox("Show Center Line", ref ShowGridCenterLine);

                    if (ShowGridCenterLine)
                    {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.DragFloat("Line Thickness", ref GridLineWidth, 1f, 1, 5);
                        DrawHelper.DrawNestIndicator(1);
                        DrawHelper.DrawColorSelector("Line Color", ref GridLineColor);
                    }

                    ImGui.NewLine();
                    ImGui.Checkbox("Draw Grid Segments", ref GridDrawSegments);

                    if (GridDrawSegments) {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.Checkbox("Draw Segment Lines", ref GridDrawSegmentLines);

                        if (GridDrawSegmentLines)
                        {
                            DrawHelper.DrawNestIndicator(2);
                            ImGui.DragFloat("Line Height", ref GridLineHeight, 1f, 1, 50);
                        }
                        ImGui.NewLine();
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.Checkbox("Draw Labels", ref GridShowSecondsText);

                        if (GridShowSecondsText)
                        {
                            DrawHelper.DrawNestIndicator(2);
                            DrawHelper.DrawFontSelector("Font##Name", ref GridFontKey, ref GridFontId);
                            DrawHelper.DrawNestIndicator(2);
                            DrawHelper.DrawColorSelector("Text Color", ref GridSegmentLabelColor);
                            DrawHelper.DrawNestIndicator(2);
                            DrawHelper.DrawColorSelector("Text Outline Color", ref GridSegmentLabelOutlineColor);
                            ImGui.NewLine();
                            DrawHelper.DrawNestIndicator(2);
                            ImGui.Checkbox("Label on Bottom / Left Side", ref GridSegmentLabelAnchorBottom);
                            DrawHelper.SetTooltip("For horizontal timelines, this places labels at the bottom. For vertical timelines, it places them on the left.");
                            DrawHelper.DrawNestIndicator(2);
                            ImGui.DragFloat("Segment Text Offset", ref GridSegmentLabelOffset, 1f, 1, 100);
                            
                        }

                        // Display and manage the list of segments using a table
                        // Sort segment list and make sure there are no duplicate segments
                        GridSegments = GridSegments.Distinct().ToList();
                        GridSegments.Sort();
                
                        ImGui.NewLine();
                        ImGui.Text("Current Segments:");
                        DrawHelper.SetTooltip("These numbers represent time in seconds from the current moment, marking significant points on your timeline (e.g., 10 seconds, 30 seconds, 1 minute, 2 minutes).");
                        ImGui.Separator();
                
                        ImGui.DragInt("", ref _segmentInput);
                        DrawHelper.SetTooltip("Enter a time in seconds to add a new grid segment to the timeline.");

                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString()) && _segmentInput > 0 && !GridSegments.Contains(_segmentInput))
                        {
                            GridSegments.Add(_segmentInput);
                        }
                        ImGui.PopFont();

                        if (ImGui.BeginTable("SegmentsTable", 2, ImGuiTableFlags.Borders))
                        {
                            ImGui.TableSetupColumn("Segment(s)", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 50f);
                            ImGui.TableHeadersRow();

                            for (int i = 0; i < GridSegments.Count; i++)
                            {
                                float segment = GridSegments[i];

                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(segment.ToString());

                                ImGui.TableNextColumn();
                                ImGui.PushFont(UiBuilder.IconFont);
                                if (ImGui.Button($"{FontAwesomeIcon.Trash.ToIconString()}##{i}"))
                                {
                                    GridSegments.RemoveAt(i);
                                    i--; // Adjust index due to removal
                                }
                                ImGui.PopFont();
                            }

                            ImGui.EndTable();
                        }
                    }
                }
            }

            ImGui.EndChild();
        }
    }
}