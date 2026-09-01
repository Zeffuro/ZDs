using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace ZDs.Helpers;

public static unsafe class EnemyListHelper
{
    private static readonly Dictionary<uint, int> EntityIdToIndex = [];

    public static void Update()
    {
        EntityIdToIndex.Clear();
        EnemyListNumberArray* enemies = EnemyListNumberArray.Instance();
        if (enemies == null)
        {
            return;
        }

        int count = enemies->EnemyCount;
        for (int index = 0; index < count; index++)
        {
            EntityIdToIndex[(uint)enemies->Enemies[index].EntityId] = index;
        }
    }

    public static char? GetMarker(uint entityId)
    {
        if (!EntityIdToIndex.TryGetValue(entityId, out int index))
        {
            return null;
        }

        EnemyListStringArray* strings = EnemyListStringArray.Instance();
        if (strings == null)
        {
            return null;
        }

        var members = strings->Members;
        if (members.IsEmpty || index >= members.Length)
        {
            return null;
        }

        string name = members[index].EnemyName.ToString();
        return string.IsNullOrEmpty(name) ? null : name[0];
    }

    public static void Clear() => EntityIdToIndex.Clear();
}
