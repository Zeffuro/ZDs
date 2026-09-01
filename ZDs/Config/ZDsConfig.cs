using System;
using System.Collections.Generic;
using ZDs.Helpers;
using Newtonsoft.Json;

namespace ZDs.Config
{
    [JsonObject]
    public class ZDsConfig : IConfigurable, IPluginDisposable
    {
        public int ConfigSchemaVersion;
        public string Version => Plugin.Version;
        public string Name 
        { 
            get => "ZDs";
            set {}
        }

        [JsonIgnore]
        private AboutPage AboutPage { get; }
        [JsonIgnore]
        private ProfilesConfig ProfilesConfig { get; }
        public GeneralConfig GeneralConfig { get; init; }
        public TimelineLayoutConfig LayoutConfig { get; init; }
        public SourcesConfig SourcesConfig { get; init; }
        public AbilitiesConfig AbilitiesConfig { get; init; }
        public CooldownConfig CooldownConfig { get; init; }
        public GridConfig GridConfig { get; init; }
        public FontConfig FontConfig { get; init; }

        public ZDsConfig()
        {
            GeneralConfig = new GeneralConfig();
            LayoutConfig = new TimelineLayoutConfig();
            SourcesConfig = new SourcesConfig();
            AbilitiesConfig = new AbilitiesConfig();
            CooldownConfig = new CooldownConfig();
            GridConfig = new GridConfig();
            FontConfig = new FontConfig();
            ProfilesConfig = new ProfilesConfig();
            AboutPage = new AboutPage();
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
                GeneralConfig.Preview = false;
                ConfigHelpers.SaveConfig(this);
            }
        }

        public IEnumerable<IConfigPage> GetConfigPages()
        {
            yield return GeneralConfig;
            yield return LayoutConfig;
            yield return SourcesConfig;
            yield return AbilitiesConfig;
            yield return CooldownConfig;
            yield return GridConfig;
            yield return FontConfig;
            yield return ProfilesConfig;
            yield return AboutPage;
        }

        public void ImportPage(IConfigPage page)
        {
        }
    }
}
