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
        
        public static void ResetConfig()
        {
            Plugin.Config = new ZDsConfig();
            SaveConfig();
        }

        public static void ExportConfig()
        {
            ConfigHelpers.ExportToClipboard<ZDsConfig>(Plugin.Config);
        }

        public static void ImportConfig()
        {
            string importString = ImGui.GetClipboardText();
            ZDsConfig? config = ConfigHelpers.GetFromImportString<ZDsConfig>(importString);

            if (config is not null)
            {
                Plugin.Config = config;
            }
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
                Plugin.Logger.Error(ex.ToString());
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
                Plugin.Logger.Error(ex.ToString());
            }

            return default;
        }

        public static ZDsConfig LoadConfig(string path)
        {
            ZDsConfig? config = null;

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
                Plugin.Logger.Error(ex.ToString());

                string backupPath = $"{path}.bak";
                if (File.Exists(path))
                {
                    try
                    {
                        File.Copy(path, backupPath);
                        Plugin.Logger.Information($"Backed up ZDs config to '{backupPath}'.");
                    }
                    catch
                    {
                        Plugin.Logger.Warning($"Unable to back up ZDs config.");
                    }
                }
            }

            return config ?? new ZDsConfig();
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
                Plugin.Logger.Error(ex.ToString());
            }
        }
    }

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