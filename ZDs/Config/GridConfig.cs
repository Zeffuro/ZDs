using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
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
        public bool GridAdaptiveLabels;
        public bool GridAutoSegments;
        public bool ShowGcdMarker;
        public bool ShowCastMarker;
        public bool ShowBuffMarkers;
        public ConfigColor GcdMarkerColor = new(0.28f, 0.78f, 1f, 0.9f);
        public ConfigColor CastMarkerColor = new(1f, 0.72f, 0.2f, 0.9f);
        public ConfigColor BuffMarkerColor = new(0.45f, 1f, 0.55f, 0.85f);
        public float GridLineWidth = 1;
        public float GridLineHeight = 15;
        public ConfigColor GridLineColor = new ConfigColor(1f, 1f, 1f, 1f);
        public List<float> GridSegments = new List<float> { 10f, 30f, 60f, 120f };
        
        public LabelTextConfig GridLabelText = new(
            FontsManager.DefaultSmallFontSize,
            new ConfigColor(1f, 1f, 1f, 1f),
            new ConfigColor(0f, 0f, 0f, 1f));

        [JsonProperty("GridFontKey")]
        private string? LegacyGridFontKey
        {
            set => GridLabelText.MigrateFont(value);
        }
        
        [JsonProperty("GridSegmentLabelColor")]
        private ConfigColor LegacyGridSegmentLabelColor
        {
            set => GridLabelText.Color = value;
        }

        [JsonProperty("GridSegmentLabelOutlineColor")]
        private ConfigColor LegacyGridSegmentLabelOutlineColor
        {
            set => GridLabelText.EffectColor = value;
        }
        public bool GridSegmentLabelsFormatDuration = true;
        
        public float GridSegmentLabelOffset = 5f;
        public bool GridSegmentLabelAnchorBottom = true;
        
        private int _segmentInput;

        public IConfigPage GetDefault() => new GridConfig();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            using var child = ImRaii.Child($"##{Name}", new Vector2(size.X, size.Y), border);
            if (!child)
            {
                return;
            }

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
                    ImGui.TextUnformatted("Markers");
                    ImGui.TextDisabled("Short colored lines across the center of the timeline.");
                    DrawHelper.DrawNestIndicator(1);
                    ImGui.Checkbox("GCD", ref ShowGcdMarker);
                    if (ShowGcdMarker)
                    {
                        DrawHelper.DrawNestIndicator(2);
                        DrawHelper.DrawColorSelector("GCD Marker Color", ref GcdMarkerColor);
                        DrawHelper.SetTooltip("Shown while the global cooldown is running.");
                    }

                    DrawHelper.DrawNestIndicator(1);
                    ImGui.Checkbox("Cast", ref ShowCastMarker);
                    if (ShowCastMarker)
                    {
                        DrawHelper.DrawNestIndicator(2);
                        DrawHelper.DrawColorSelector("Cast Marker Color", ref CastMarkerColor);
                        DrawHelper.SetTooltip("Shown while you are casting.");
                    }

                    DrawHelper.DrawNestIndicator(1);
                    ImGui.Checkbox("Buffs", ref ShowBuffMarkers);
                    if (ShowBuffMarkers)
                    {
                        DrawHelper.DrawNestIndicator(2);
                        DrawHelper.DrawColorSelector("Buff Marker Color", ref BuffMarkerColor);
                        DrawHelper.SetTooltip("Shown at the end of each tracked buff.");
                    }

                    if (ShowGcdMarker || ShowCastMarker || ShowBuffMarkers)
                    {
                        DrawMarkerPreview();
                    }

                    ImGui.NewLine();
                    ImGui.Checkbox("Draw Grid Segments", ref GridDrawSegments);

                    if (GridDrawSegments) {
                        DrawHelper.DrawNestIndicator(1);
                        ImGui.Checkbox("Automatic Segments", ref GridAutoSegments);
                        DrawHelper.SetTooltip("Choose clear time marks for the current size and range.");
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
                            DrawHelper.DrawLabelTextConfig("GridLabels", GridLabelText, 2);
                            ImGui.NewLine();
                            
                            DrawHelper.DrawNestIndicator(2);
                            ImGui.Checkbox("Duration in minutes", ref GridSegmentLabelsFormatDuration);
                            DrawHelper.DrawNestIndicator(2);
                            ImGui.Checkbox("Avoid Overlapping Labels", ref GridAdaptiveLabels);
                            DrawHelper.SetTooltip("Hide labels that do not fit cleanly.");
                            DrawHelper.DrawNestIndicator(2);
                            ImGui.Checkbox("Label on Bottom / Left Side", ref GridSegmentLabelAnchorBottom);
                            DrawHelper.SetTooltip("For horizontal timelines, this places labels at the bottom. For vertical timelines, it places them on the left.");
                            DrawHelper.DrawNestIndicator(2);
                            ImGui.DragFloat("Segment Text Offset", ref GridSegmentLabelOffset, 1f, 1, 100);
                            
                        }

                        if (!GridAutoSegments)
                        {
                            GridSegments = GridSegments.Distinct().ToList();
                            GridSegments.Sort();

                            ImGui.NewLine();
                            ImGui.Text("Current Segments:");
                            DrawHelper.SetTooltip("Time marks shown on the timeline.");
                            ImGui.Separator();

                            ImGui.DragInt("", ref _segmentInput);
                            DrawHelper.SetTooltip("Enter a time in seconds.");

                            ImGui.SameLine();
                            using (ImRaii.PushFont(UiBuilder.IconFont))
                            {
                                if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString()) && _segmentInput > 0 && !GridSegments.Contains(_segmentInput))
                                {
                                    GridSegments.Add(_segmentInput);
                                }
                            }

                            using var table = ImRaii.Table("SegmentsTable", 2, ImGuiTableFlags.Borders);
                            if (table)
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
                                    using (ImRaii.PushFont(UiBuilder.IconFont))
                                    {
                                        if (ImGui.Button($"{FontAwesomeIcon.Trash.ToIconString()}##{i}"))
                                        {
                                            GridSegments.RemoveAt(i);
                                            i--;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

        }

        private void DrawMarkerPreview()
        {
            ImGui.NewLine();
            DrawHelper.DrawNestIndicator(1);
            Vector2 start = ImGui.GetCursorScreenPos();
            float width = MathF.Min(300f, ImGui.GetContentRegionAvail().X);
            Vector2 size = new(width, 38f);
            ImGui.Dummy(size);

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector4 background = KnownColor.Black.Vector();
            background.W = 0.28f;
            drawList.AddRectFilled(start, start + size, ImGui.GetColorU32(background), 4f);

            float centerY = start.Y + 13f;
            drawList.AddLine(
                new Vector2(start.X + 10f, centerY),
                new Vector2(start.X + size.X - 10f, centerY),
                GridLineColor.Base,
                MathF.Max(1f, GridLineWidth));

            (bool Enabled, string Label, uint Color)[] markers =
            [
                (ShowGcdMarker, "GCD", GcdMarkerColor.Base),
                (ShowCastMarker, "Cast", CastMarkerColor.Base),
                (ShowBuffMarkers, "Buff", BuffMarkerColor.Base),
            ];
            var visible = markers.Where(marker => marker.Enabled).ToArray();
            for (int index = 0; index < visible.Length; index++)
            {
                float x = start.X + size.X * (index + 1f) / (visible.Length + 1f);
                drawList.AddLine(new Vector2(x, centerY - 8f), new Vector2(x, centerY + 8f), visible[index].Color, 3f);
                Vector2 labelSize = ImGui.CalcTextSize(visible[index].Label);
                drawList.AddText(new Vector2(x - labelSize.X / 2f, centerY + 10f), visible[index].Color, visible[index].Label);
            }
        }
    }
}
