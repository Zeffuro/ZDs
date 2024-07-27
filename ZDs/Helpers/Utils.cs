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
        
        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                try
                {
                    // hack because of this: https://github.com/dotnet/corefx/issues/10361
                    if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
                    {
                        url = url.Replace("&", "^&");
                        Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start("xdg-open", url);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger.Error("Error trying to open url: " + e.Message);
                }
            }
        }
    }
}
