using ZDs.Config;
using Dalamud.Plugin.Services;

namespace ZDs.Helpers;

public static class ConfigMigration
{
    public const int CurrentSchemaVersion = 1;

    public static ZDsConfig CreateNewDefault(string presetId = BuiltInPresetCatalog.RecommendedPresetId)
    {
        ZDsConfig config = new()
        {
            ConfigSchemaVersion = CurrentSchemaVersion,
        };
        BuiltInPresetCatalog.ApplyTo(config, presetId);
        return config;
    }

    public static void Migrate(ZDsConfig config)
    {
        if (config.ConfigSchemaVersion > CurrentSchemaVersion)
        {
            IPluginLog.Get().Warning(
                "Configuration schema {ConfigSchemaVersion} is newer than supported schema {CurrentSchemaVersion}.",
                config.ConfigSchemaVersion,
                CurrentSchemaVersion);
            config.LayoutConfig.Normalize(config.GeneralConfig.TimelineTime);
            return;
        }

        while (config.ConfigSchemaVersion < CurrentSchemaVersion)
        {
            switch (config.ConfigSchemaVersion)
            {
                case <= 0:
                    config.ConfigSchemaVersion = 1;
                    break;
                default:
                    config.ConfigSchemaVersion = CurrentSchemaVersion;
                    break;
            }
        }

        config.LayoutConfig.Normalize(config.GeneralConfig.TimelineTime);
    }
}
