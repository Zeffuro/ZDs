using System.Numerics;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
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
            using var child = ImRaii.Child("##AboutPage", new Vector2(size.X, size.Y), border);
            if (!child)
            {
                return;
            }

            ImGui.Text("Changelog");
            Vector2 changeLogSize = new(size.X - padX * 2, size.Y - ImGui.GetCursorPosY() - padY - 30);

                using (var changelog = ImRaii.Child("##Changelog", changeLogSize, true))
                {
                    if (changelog)
                    {
                        ImGui.Text(Plugin.Changelog);
                    }
                }

                using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0);
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

        }
    }
}
