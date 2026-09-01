using System.Collections.Generic;

namespace ZDs.Timeline;

internal static class TimelineActionCatalog
{
    public static readonly IReadOnlyDictionary<uint, uint> CanonicalIds = new Dictionary<uint, uint>
    {
        [110] = 110,
        [117] = 110,
        [36975] = 110,
        [16498] = 16498,
        [16499] = 16498,
        [16475] = 53,
        [2259] = 2259,
        [2261] = 2259,
        [2263] = 2259,
        [18805] = 18805,
        [18806] = 18805,
        [18807] = 18805,
        [16484] = 7477,
        [16485] = 7477,
        [16486] = 7477,
        [25858] = 7504,
        [16527] = 16527,
        [7515] = 16527,
        [24380] = 24380,
        [24381] = 24380,
        [34620] = 34620,
        [34623] = 34620,
    };

    public static readonly IReadOnlyDictionary<uint, float> RecastOverrides = new Dictionary<uint, float>
    {
        [2259] = 0.5f,
        [2261] = 0.5f,
        [2263] = 0.5f,
        [18805] = 0.5f,
        [18806] = 0.5f,
        [18807] = 0.5f,
        [2265] = 1.5f,
        [2266] = 1.5f,
        [2267] = 1.5f,
        [2268] = 1.5f,
        [2269] = 1.5f,
        [2270] = 1.5f,
        [2271] = 1.5f,
        [2272] = 1.5f,
        [16491] = 1.5f,
        [16492] = 1.5f,
    };

    public static readonly IReadOnlySet<uint> MudraActions = new HashSet<uint>
    {
        2259,
        2261,
        2263,
    };

    public static readonly IReadOnlyDictionary<uint, uint> IconOverrides = new Dictionary<uint, uint>
    {
        [1] = 101,
        [3] = 104,
        [2259] = 2904,
        [2261] = 2904,
        [2263] = 2904,
        [18805] = 2904,
        [18806] = 2904,
        [18807] = 2904,
    };

    public static readonly (uint ActionId, TimelineItemType Type)[] PreviewActions =
    [
        (110, TimelineItemType.OffGCD),
        (2874, TimelineItemType.OffGCD),
        (7561, TimelineItemType.OffGCD),
        (7562, TimelineItemType.OffGCD),
        (7531, TimelineItemType.OffGCD),
        (34675, TimelineItemType.OffGCD),
        (43, TimelineItemType.OffGCD),
    ];
}
