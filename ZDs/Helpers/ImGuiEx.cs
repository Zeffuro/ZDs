using Dalamud.Bindings.ImGui;
using System;

namespace ZDs.Helpers
{
    public static class ImGuiEx
    {
        public static bool EnumCombo<T>(string label, ref T currentValue, string[] labels) where T : Enum
        {
            int index = Convert.ToInt32(currentValue);
            bool changed = ImGui.Combo(label, ref index, labels, labels.Length);
            if (changed)
                currentValue = (T)Enum.ToObject(typeof(T), index);
            return changed;
        }
    }
}
