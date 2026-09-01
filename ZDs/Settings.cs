using Dalamud.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ZDs.Helpers;

namespace ZDs
{
    public class Settings
    {
        

        public bool ShowingModalWindow = false;

        #region load / save
        private static string JsonPath = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "Settings.json");
        public static Settings Load()
        {
            string path = JsonPath;
            Settings? settings = null;

            try
            {
                if (File.Exists(path))
                {
                    string jsonString = File.ReadAllText(path);
                    settings = JsonConvert.DeserializeObject<Settings>(jsonString);
                }
            }
            catch (Exception e)
            {
                IPluginLog.Get().Error(e, "Error reading settings file.");
            }

            if (settings == null)
            {
                settings = new Settings();
                Save(settings);
            }
            
            // Ensure GridSegments are unique and no defaults are erroneously added
            //settings.GridSegments = settings.GridSegments.Distinct().ToList();

            return settings;
        }

        public static void Save(Settings settings)
        {
            try
            {
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                    TypeNameHandling = TypeNameHandling.Objects
                };
                string jsonString = JsonConvert.SerializeObject(settings, Formatting.Indented, serializerSettings);

                File.WriteAllText(JsonPath, jsonString);
            }
            catch (Exception e)
            {
                IPluginLog.Get().Error(e, "Error saving settings file.");
            }
        }
        #endregion
    }
}
