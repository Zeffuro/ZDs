using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace ZDs.Helpers;

internal readonly ref struct ImGuiClipRectScope
{
    public ImGuiClipRectScope(Vector2 minimum, Vector2 maximum, bool intersectWithCurrent = false)
    {
        ImGui.PushClipRect(minimum, maximum, intersectWithCurrent);
    }

    public void Dispose() => ImGui.PopClipRect();
}
