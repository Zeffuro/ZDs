using System;
using System.Collections.Generic;
using ZDs.Helpers;
using Newtonsoft.Json;

namespace ZDs.Config
{
    [JsonObject]
    public class ZDsConfig : IConfigurable, IPluginDisposable
    {
        public bool FirstLoad = true;
        public string Version => Plugin.Version;
        public string Name 
        { 
            get => "ZDs";
            set {}
        }

        [JsonIgnore]
        //private AboutPage AboutPage { get; } = new AboutPage();

        public GeneralConfig GeneralConfig { get; init; }
        public AbilitiesConfig AbilitiesConfig { get; init; }
        public CooldownConfig CooldownConfig { get; init; }
        public GridConfig GridConfig { get; init; }
        public FontConfig FontConfig { get; init; }

        public ZDsConfig()
        {
            GeneralConfig = new GeneralConfig();
            AbilitiesConfig = new AbilitiesConfig();
            CooldownConfig = new CooldownConfig();
            GridConfig = new GridConfig();
            FontConfig = new FontConfig();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ConfigHelpers.SaveConfig(this);
            }
        }

        public IEnumerable<IConfigPage> GetConfigPages()
        {
            yield return GeneralConfig;
            yield return AbilitiesConfig;
            yield return CooldownConfig;
            yield return GridConfig;
            yield return FontConfig;
            //yield return this.AboutPage;
        }

        public void ImportPage(IConfigPage page)
        {
        }
    }
}
