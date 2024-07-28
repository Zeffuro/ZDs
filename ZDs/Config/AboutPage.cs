using System.Numerics;
using System.Text.Json.Serialization;
using ImGuiNET;
using ZDs.Helpers;

namespace ZDs.Config
{
    public class AboutPage : IConfigPage
    {
        [JsonIgnore]
        public bool Active { get; set; }

        public string Name => "About";

        public IConfigPage GetDefault() => new AboutPage();

        public void DrawConfig(Vector2 size, float padX, float padY, bool border = true)
        {
            if (ImGui.BeginChild("##AboutPage", new Vector2(size.X, size.Y), border))
            {
                ImGui.Text("Changelog");
                Vector2 changeLogSize = new(size.X - padX * 2, size.Y - ImGui.GetCursorPosY() - padY - 30);

                if (ImGui.BeginChild("##Changelog", changeLogSize, true))
                {
                    ImGui.Text(Plugin.Changelog);
                    ImGui.EndChild();
                }

                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                Vector2 buttonSize = new((size.X - padX * 2 - padX * 2) / 3, 30 - padY * 2);
                if (ImGui.Button("Github", buttonSize))
                {
                    Dalamud.Utility.Util.OpenLink("https://github.com/Zeffuro/ZDs");
                }

                ImGui.SameLine();
                if (ImGui.Button("Ko-fi", buttonSize))
                {
                    Dalamud.Utility.Util.OpenLink("https://ko-fi.com/Zeffuro");
                }

                ImGui.PopStyleVar();
            }

            ImGui.EndChild();
        }
    }
}