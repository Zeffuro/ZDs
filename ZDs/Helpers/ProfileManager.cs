using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ZDs.Config;
using Dalamud.Plugin.Services;

namespace ZDs.Helpers
{
    public enum OnboardingAudience
    {
        None = 0,
        NewUser = 1,
        ExistingUser = 2,
    }

    internal sealed class ProfileStore
    {
        public int StoreVersion { get; set; }
        public int OnboardingVersion { get; set; }
        public OnboardingAudience PendingOnboarding { get; set; }
        public string? LastAppliedPresetId { get; set; }
        public int LastAppliedPresetRevision { get; set; }
        public int MigrationBackupVersion { get; set; }
        public string? MigrationBackupProfileName { get; set; }
        public string ActiveProfile { get; set; } = "Default";
        public Dictionary<string, string> Profiles { get; set; } = [];
    }

    public static class ProfileManager
    {
        public const int CurrentOnboardingVersion = 1;
        private const string ProfilesFileName = "Profiles.json";
        private static ProfileStore _store = new();

        private static string ProfilesPath => Path.Combine(Plugin.ConfigFileDir, ProfilesFileName);
        public static string ActiveProfile => _store.ActiveProfile;
        public static IReadOnlyList<string> ProfileNames => _store.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        public static OnboardingAudience PendingOnboarding => _store.PendingOnboarding;
        public static string? MigrationBackupProfileName => _store.MigrationBackupProfileName;

        public static void Initialize(bool isNewInstall)
        {
            bool storeExisted = File.Exists(ProfilesPath);
            _store = new ProfileStore();
            try
            {
                if (File.Exists(ProfilesPath))
                {
                    _store = JsonConvert.DeserializeObject<ProfileStore>(File.ReadAllText(ProfilesPath)) ?? new ProfileStore();
                }
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "Failed to load ZDs profiles.");
                _store = new ProfileStore();
            }

            _store.Profiles ??= [];
            if (_store.Profiles.Count == 0)
            {
                SaveCurrent("Default", true);
            }

            if (_store.Profiles.Count > 0)
            {
                _store.ActiveProfile ??= string.Empty;
                string? activeKey = FindKey(_store.ActiveProfile);
                _store.ActiveProfile = activeKey ?? _store.Profiles.Keys.First();
            }

            if (_store.OnboardingVersion < CurrentOnboardingVersion && _store.PendingOnboarding == OnboardingAudience.None)
            {
                _store.PendingOnboarding = isNewInstall && !storeExisted
                    ? OnboardingAudience.NewUser
                    : OnboardingAudience.ExistingUser;
            }

            if (isNewInstall && !storeExisted)
            {
                _store.MigrationBackupVersion = CurrentOnboardingVersion;
            }
            else if (_store.MigrationBackupVersion < CurrentOnboardingVersion)
            {
                string backupName = GetUniqueName("Before timeline update");
                if (SaveCurrent(backupName, false))
                {
                    _store.MigrationBackupVersion = CurrentOnboardingVersion;
                    _store.MigrationBackupProfileName = backupName;
                }
            }

            if (isNewInstall && string.IsNullOrEmpty(_store.LastAppliedPresetId))
            {
                _store.LastAppliedPresetId = BuiltInPresetCatalog.RecommendedPresetId;
                _store.LastAppliedPresetRevision = BuiltInPresetCatalog.CurrentRevision;
            }

            _store.StoreVersion = 1;
            SaveStore();
        }

        public static bool SaveCurrent(string name, bool makeActive = true)
        {
            name = NormalizeName(name);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string? snapshot = ConfigHelpers.GetExportString(Plugin.Config);
            if (snapshot is null)
            {
                return false;
            }

            string key = FindKey(name) ?? name;
            _store.Profiles[key] = snapshot;
            if (makeActive)
            {
                _store.ActiveProfile = key;
            }

            ConfigHelpers.SaveConfig();
            SaveStore();
            return true;
        }

        public static bool Switch(string name)
        {
            string? key = FindKey(name);
            if (key is null)
            {
                return false;
            }

            if (!string.Equals(_store.ActiveProfile, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_store.ActiveProfile))
            {
                SaveCurrent(_store.ActiveProfile, false);
            }

            ZDsConfig? config = ConfigHelpers.GetFromImportString<ZDsConfig>(_store.Profiles[key]);
            if (config is null)
            {
                return false;
            }

            ConfigMigration.Migrate(config);
            config.GeneralConfig.TimelineLocked = true;
            _store.ActiveProfile = key;
            Plugin.ApplyConfig(config);
            SaveStore();
            return true;
        }

        public static bool Import(string exportString)
        {
            ZDsConfig? imported = ConfigHelpers.GetFromImportString<ZDsConfig>(exportString);
            if (imported is null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_store.ActiveProfile) && !SaveCurrent(_store.ActiveProfile, false))
            {
                return false;
            }

            ConfigMigration.Migrate(imported);
            imported.GeneralConfig.TimelineLocked = true;
            Plugin.ApplyConfig(imported);
            return SaveCurrent(GetUniqueName("Imported"), true);
        }

        public static bool Delete(string name)
        {
            string? key = FindKey(name);
            if (key is null || string.Equals(key, _store.ActiveProfile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool removed = _store.Profiles.Remove(key);
            if (removed)
            {
                SaveStore();
            }

            return removed;
        }

        public static void SaveActive()
        {
            if (!string.IsNullOrEmpty(_store.ActiveProfile))
            {
                SaveCurrent(_store.ActiveProfile, false);
            }
        }

        public static bool ApplyBuiltInPreset(string presetId, bool createProfile)
        {
            BuiltInPreset? preset = BuiltInPresetCatalog.Find(presetId);
            if (preset is null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_store.ActiveProfile))
            {
                if (!SaveCurrent(_store.ActiveProfile, false))
                {
                    return false;
                }
            }

            string? snapshot = ConfigHelpers.GetExportString(Plugin.Config);
            ZDsConfig? updated = snapshot is null
                ? null
                : ConfigHelpers.GetFromImportString<ZDsConfig>(snapshot);
            if (updated is null || !BuiltInPresetCatalog.ApplyTo(updated, presetId))
            {
                return false;
            }

            updated.GeneralConfig.TimelineLocked = true;
            ConfigMigration.Migrate(updated);
            Plugin.ApplyConfig(updated);
            _store.LastAppliedPresetId = preset.Id;
            _store.LastAppliedPresetRevision = BuiltInPresetCatalog.CurrentRevision;

            string profileName = createProfile
                ? GetUniqueName(preset.Name)
                : _store.ActiveProfile;
            return SaveCurrent(profileName, true);
        }

        public static void CompleteOnboarding()
        {
            _store.OnboardingVersion = CurrentOnboardingVersion;
            _store.PendingOnboarding = OnboardingAudience.None;
            SaveStore();
        }

        private static string NormalizeName(string name)
        {
            string trimmed = name.Trim();
            return trimmed.Length <= 64 ? trimmed : trimmed[..64];
        }

        private static string? FindKey(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return _store.Profiles.Keys.FirstOrDefault(key => string.Equals(key, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static string GetUniqueName(string baseName)
        {
            string normalized = NormalizeName(baseName);
            if (FindKey(normalized) is null)
            {
                return normalized;
            }

            for (int suffix = 2; suffix < 1000; suffix++)
            {
                string candidate = NormalizeName($"{baseName} {suffix}");
                if (FindKey(candidate) is null)
                {
                    return candidate;
                }
            }

            return NormalizeName($"{baseName} {DateTime.Now:yyyyMMdd-HHmmss}");
        }

        private static void SaveStore()
        {
            try
            {
                Directory.CreateDirectory(Plugin.ConfigFileDir);
                File.WriteAllText(ProfilesPath, JsonConvert.SerializeObject(_store, Formatting.Indented));
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "Failed to save ZDs profiles.");
            }
        }
    }
}
