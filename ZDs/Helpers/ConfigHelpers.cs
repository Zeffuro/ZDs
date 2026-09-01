using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using ZDs.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ZDs.Helpers
{
    public static class ConfigHelpers
    {
        private static readonly JsonSerializerSettings _serializerSettings = new()
        {
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.Objects,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            SerializationBinder = new ZDsSerializationBinder()
        };
        
        public static bool ResetConfig()
        {
            try
            {
                if (Directory.Exists(Plugin.ConfigFileDir))
                {
                    Directory.Delete(Plugin.ConfigFileDir, true);
                }

                Directory.CreateDirectory(Plugin.ConfigFileDir);
                FontsManager.CopyPluginFontsToUserPath();
                ZDsConfig freshConfig = ConfigMigration.CreateNewDefault();
                freshConfig.GeneralConfig.TimelineLocked = false;
                Plugin.ApplyConfig(freshConfig, false);
                ProfileManager.Initialize(true);
                SaveConfig();
                Plugin.ShowSetupAfterReset();
                DrawHelper.DrawNotification("ZDs settings were reset.", NotificationType.Success);
                return true;
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "Failed to reset ZDs settings.");
                DrawHelper.DrawNotification("Could not reset ZDs settings.", NotificationType.Error);
                return false;
            }
        }

        public static void ExportConfig()
        {
            ConfigHelpers.ExportToClipboard<ZDsConfig>(Plugin.Config);
        }

        public static void ExportToClipboard<T>(T toExport)
        {
            string? exportString = GetExportString(toExport);

            if (exportString is not null)
            {
                ImGui.SetClipboardText(exportString);
                DrawHelper.DrawNotification("Export string copied to clipboard.");
            }
            else
            {
                DrawHelper.DrawNotification("Failed to Export!", NotificationType.Error);
            }
        }

        public static string? GetExportString<T>(T toExport)
        {
            try
            {
                string jsonString = JsonConvert.SerializeObject(toExport, Formatting.None, _serializerSettings);
                using (MemoryStream outputStream = new())
                {
                    using (DeflateStream compressionStream = new(outputStream, CompressionLevel.Optimal))
                    {
                        using (StreamWriter writer = new(compressionStream, Encoding.UTF8))
                        {
                            writer.Write(jsonString);
                        }
                    }

                    return Convert.ToBase64String(outputStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "Failed to export configuration.");
            }

            return null;
        }

        public static T? GetFromImportString<T>(string importString)
        {
            if (string.IsNullOrEmpty(importString)) return default;

            try
            {
                byte[] bytes = Convert.FromBase64String(importString);

                string decodedJsonString;
                using (MemoryStream inputStream = new(bytes))
                {
                    using (DeflateStream compressionStream = new(inputStream, CompressionMode.Decompress))
                    {
                        using (StreamReader reader = new(compressionStream, Encoding.UTF8))
                        {
                            decodedJsonString = reader.ReadToEnd();
                        }
                    }
                }

                T? importedObj = JsonConvert.DeserializeObject<T>(decodedJsonString, _serializerSettings);
                return importedObj;
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "Failed to import configuration.");
            }

            return default;
        }

        public static ConfigLoadResult LoadConfig(string path)
        {
            ZDsConfig? config = null;
            bool fileExisted = File.Exists(path);

            try
            {
                if (File.Exists(path))
                {
                    string jsonString = File.ReadAllText(path);
                    config = JsonConvert.DeserializeObject<ZDsConfig>(jsonString, _serializerSettings);
                }
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "Failed to load configuration.");

                string backupPath = $"{path}.bak";
                if (File.Exists(path))
                {
                    try
                    {
                        File.Copy(path, backupPath);
                        IPluginLog.Get().Information("Backed up ZDs config to '{BackupPath}'.", backupPath);
                    }
                    catch
                    {
                        IPluginLog.Get().Warning("Unable to back up ZDs config.");
                    }
                }
            }

            if (config is null)
            {
                if (!fileExisted)
                {
                    return new ConfigLoadResult(ConfigMigration.CreateNewDefault(), true);
                }

                ZDsConfig recovered = new();
                ConfigMigration.Migrate(recovered);
                return new ConfigLoadResult(recovered, false);
            }

            ConfigMigration.Migrate(config);
            return new ConfigLoadResult(config, false);
        }

        public static void SaveConfig()
        {
            SaveConfig(Plugin.Config);
        }

        public static void SaveConfig(ZDsConfig config)
        {
            try
            {
                string jsonString = JsonConvert.SerializeObject(config, Formatting.Indented, _serializerSettings);
                File.WriteAllText(Plugin.ConfigFilePath, jsonString);
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "Failed to save configuration.");
            }
        }
    }

    public readonly record struct ConfigLoadResult(ZDsConfig Config, bool IsNewInstall);

    /// <summary>
    /// Because the game blocks the json serializer from loading assemblies at runtime, we define
    /// a custom SerializationBinder to ignore the assembly name for the types defined by this plugin.
    /// </summary>
    public class ZDsSerializationBinder : ISerializationBinder
    {
        private static readonly List<Type> _configTypes =
        [
        ];

        private static readonly Dictionary<string, string> _typeNameConversions = new()
        {
        };

        private readonly Dictionary<Type, string> _typeToName = [];
        private readonly Dictionary<string, Type> _nameToType = [];

        public ZDsSerializationBinder()
        {
            foreach (Type type in _configTypes)
            {
                if (type.FullName is not null)
                {
                    _typeToName.Add(type, type.FullName.ToLower());
                    _nameToType.Add(type.FullName.ToLower(), type);
                }
            }
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            if (_typeToName.TryGetValue(serializedType, out string? name))
            {
                assemblyName = null;
                typeName = name;
            }
            else
            {
                assemblyName = serializedType.Assembly.FullName;
                typeName = serializedType.FullName;
            }
        }

        public Type BindToType(string? assemblyName, string? typeName)
        {
            if (typeName is null)
            {
                throw new TypeLoadException("Type name was null.");
            }

            if (_nameToType.TryGetValue(typeName.ToLower(), out Type? type))
            {
                return type;
            }

            Type? loadedType = Type.GetType($"{typeName}, {assemblyName}", false);
            if (loadedType is null)
            {
                foreach (var entry in _typeNameConversions)
                {
                    if (typeName.Contains(entry.Key))
                    {
                        typeName = typeName.Replace(entry.Key, entry.Value);
                    }
                }
            }

            return Type.GetType($"{typeName}, {assemblyName}", true) ??
                throw new TypeLoadException($"Unable to load type '{typeName}' from assembly '{assemblyName}'");
        }
    }
}
