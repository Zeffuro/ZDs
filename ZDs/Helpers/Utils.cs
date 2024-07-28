using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ZDs.Helpers
{
    public static class Utils
    {
        public static string DurationToString(double duration, int decimalCount = 0)
        {
            if (duration == 0)
            {
                return "";
            }

            TimeSpan t = TimeSpan.FromSeconds(duration);

            if (t.Hours >= 1) { return t.Hours + "h"; }
            if (t.Minutes >= 5) { return t.Minutes + "m"; }
            if (t.Minutes >= 1) { return $"{t.Minutes}:{t.Seconds:00}"; }

            return duration.ToString("N" + decimalCount);
        }
    }
}
