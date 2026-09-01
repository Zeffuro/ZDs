using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;

namespace ZDs.Helpers
{
    public enum FontSourceKind
    {
        CustomFile = 0,
        Game = 1
    }

    public struct FontData(
        string name,
        string path,
        int size,
        bool chinese,
        bool korean,
        FontSourceKind source = FontSourceKind.CustomFile,
        string? gameFamily = null)
    {
        public string Name = name;
        public string Path = path;
        public int Size = size;
        public bool Chinese = chinese;
        public bool Korean = korean;
        public FontSourceKind Source = source;
        public string? GameFamily = gameFamily;
    }

    internal sealed class EmptyFontScope : IDisposable
    {
        public static readonly EmptyFontScope Instance = new();
        public void Dispose() { }
    }

    // From LMeter https://github.com/lichie567/LMeter/blob/develop/LMeter/Helpers/FontsManager.cs
    public class FontsManager : IDisposable
    {
        private readonly Dictionary<string, IFontHandle> _imGuiFonts = [];
        private readonly Dictionary<string, string> _handleFamilies = [];
        private readonly Dictionary<string, FontData> _fontSources = [];
        private string[] _fontList;
        private readonly IUiBuilder _uiBuilder;

        public const string DalamudFontKey = "Dalamud Font";
        public const string DefaultFontFamily = "Expressway";
        public const int DefaultBigFontSize = 24;
        public const int DefaultMediumFontSize = 20;
        public const int DefaultSmallFontSize = 16;

        public FontsManager(IUiBuilder uiBuilder, IEnumerable<FontData> fonts)
        {
            _uiBuilder = uiBuilder;
            _fontList = [DalamudFontKey];
            this.BuildFonts(fonts);
        }

        private void BuildFonts(IEnumerable<FontData> fontData)
        {
            Dictionary<string, FontData> requestedSources = fontData
                .Where(IsValidFontSource)
                .GroupBy(GetFontFamilyKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (string removedKey in _handleFamilies
                         .Where(pair => !requestedSources.ContainsKey(pair.Value))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _imGuiFonts[removedKey].Dispose();
                _imGuiFonts.Remove(removedKey);
                _handleFamilies.Remove(removedKey);
            }

            _fontSources.Clear();
            foreach ((string key, FontData source) in requestedSources)
            {
                _fontSources[key] = source;
            }

            _fontList = [DalamudFontKey, .. _fontSources.Keys.Order(StringComparer.OrdinalIgnoreCase)];
        }

        private static bool IsValidFontSource(FontData font)
            => font.Source != FontSourceKind.Game
               || (!string.IsNullOrEmpty(font.GameFamily)
                   && Enum.TryParse(font.GameFamily, out GameFontFamily family)
                   && family != GameFontFamily.Undefined);

        private IFontHandle? CreateFileFontHandle(FontData font, string fontDir, int size)
        {
            string path = string.IsNullOrEmpty(font.Path) ? $"{fontDir}{font.Name}.ttf" : font.Path;
            if (!File.Exists(path))
            {
                return null;
            }

            return _uiBuilder.FontAtlas.NewDelegateFontHandle
            (
                e => e.OnPreBuild
                (
                    tk => tk.AddFontFromFile
                    (
                        path,
                        new SafeFontConfig
                        {
                            SizePx = size,
                            GlyphRanges = GetCharacterRanges(font.Chinese, font.Korean),
                        }
                    )
                )
            );
        }

        private IFontHandle? CreateGameFontHandle(FontData font, int size)
        {
            if (string.IsNullOrEmpty(font.GameFamily)
                || !Enum.TryParse(font.GameFamily, out GameFontFamily family)
                || family == GameFontFamily.Undefined)
            {
                return null;
            }

            return _uiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(family, size));
        }

        public static FontData[] GetDefaultFontData()
        {
            return [new(DefaultFontFamily, $"{GetUserFontPath()}{DefaultFontFamily}.ttf", DefaultMediumFontSize, false, false)];
        }

        public void UpdateFonts(IEnumerable<FontData> fonts)
        {
            this.BuildFonts(fonts);
        }

        public bool HasFont(string key) => _fontSources.ContainsKey(NormalizeFontFamily(key));

        private void DisposeFontHandles()
        {
            foreach ((string _, IFontHandle value) in _imGuiFonts)
            {
                value.Dispose();
            }

            _imGuiFonts.Clear();
            _handleFamilies.Clear();
            _fontSources.Clear();
        }

        private unsafe ushort[]? GetCharacterRanges(bool chinese, bool korean)
        {
            if (!chinese && !korean)
            {
                return null;
            }

            ImGuiIOPtr io = ImGui.GetIO();
            using (ImGuiHelpers.NewFontGlyphRangeBuilderPtrScoped(out ImFontGlyphRangesBuilderPtr builder))
            {
                if (chinese)
                {
                    // GetGlyphRangesChineseFull() includes Default + Hiragana, Katakana, Half-Width, Selection of 1946 Ideographs
                    // https://skia.googlesource.com/external/github.com/ocornut/imgui/+/v1.53/extra_fonts/README.txt
                    builder.AddRanges(io.Fonts.GetGlyphRangesChineseFull());
                }

                if (korean)
                {
                    builder.AddRanges(io.Fonts.GetGlyphRangesKorean());
                }

                return builder.BuildRangesToArray();
            }
        }

        public static int GetFontIndex(string fontKey)
        {
            FontsManager manager = Singletons.Get<FontsManager>();
            for (int i = 0; i < manager._fontList.Length; i++)
            {
                if (manager._fontList[i].Equals(fontKey))
                {
                    return i;
                }
            }

            return 0;
        }

        public static bool ValidateFont(string[] fontOptions, int fontId, string fontKey)
        {
            return fontId < fontOptions.Length && fontOptions[fontId].Equals(fontKey);
        }

        public static IDisposable PushFont(string fontFamily, int fontSize)
        {
            FontsManager manager = Singletons.Get<FontsManager>();
            if (string.IsNullOrWhiteSpace(fontFamily) || fontFamily == DalamudFontKey)
            {
                return EmptyFontScope.Instance;
            }

            if (TryParseFontReference(fontFamily, out string migratedFamily, out int migratedSize))
            {
                fontFamily = migratedFamily;
                if (migratedSize > 0)
                {
                    fontSize = migratedSize;
                }
            }

            fontSize = Math.Clamp(fontSize, 1, 200);
            string handleKey = $"{fontFamily}_{fontSize}";
            if (!manager._imGuiFonts.TryGetValue(handleKey, out IFontHandle? fontHandle))
            {
                if (!manager._fontSources.TryGetValue(fontFamily, out FontData source))
                {
                    return EmptyFontScope.Instance;
                }

                try
                {
                    fontHandle = source.Source switch
                    {
                        FontSourceKind.Game => manager.CreateGameFontHandle(source, fontSize),
                        _ => manager.CreateFileFontHandle(source, GetUserFontPath(), fontSize)
                    };

                    if (fontHandle is null)
                    {
                        return EmptyFontScope.Instance;
                    }

                    manager._imGuiFonts[handleKey] = fontHandle;
                    manager._handleFamilies[handleKey] = fontFamily;
                }
                catch (Exception ex)
                {
                    IPluginLog.Get().Error(ex, "Failed to load font {FontKey}.", handleKey);
                    return EmptyFontScope.Instance;
                }
            }

            return fontHandle.Push();
        }

        public static string[] GetFontList()
        {
            return Singletons.Get<FontsManager>()._fontList;
        }

        public static string GetFontFamilyKey(FontData font)
        {
            if (font.Source == FontSourceKind.Game)
            {
                return $"FFXIV_{font.GameFamily}";
            }

            string key = font.Name;
            key += font.Chinese ? "_cnjp" : string.Empty;
            key += font.Korean ? "_kr" : string.Empty;
            return key;
        }

        public static string GetFontKey(FontData font) => GetFontFamilyKey(font);

        public static string NormalizeFontFamily(string fontReference)
            => TryParseFontReference(fontReference, out string family, out _) ? family : fontReference;

        public static bool TryParseFontReference(string? fontReference, out string family, out int size)
        {
            family = DalamudFontKey;
            size = 0;
            if (string.IsNullOrWhiteSpace(fontReference))
            {
                return false;
            }

            if (fontReference.Equals(DalamudFontKey, StringComparison.Ordinal))
            {
                family = DalamudFontKey;
                return true;
            }

            string[] legacyGameParts = fontReference.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (legacyGameParts.Length == 3
                && legacyGameParts[0].Equals("ffxiv", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(legacyGameParts[2], out size))
            {
                family = $"FFXIV_{legacyGameParts[1]}";
                return true;
            }

            string[] parts = fontReference.Split('_', StringSplitOptions.RemoveEmptyEntries);
            for (int i = parts.Length - 1; i > 0; i--)
            {
                if (!int.TryParse(parts[i], out size))
                {
                    continue;
                }

                family = string.Join('_', parts.Take(i).Concat(parts.Skip(i + 1)));
                return true;
            }

            family = fontReference;
            size = 0;
            return true;
        }

        public static void CopyPluginFontsToUserPath()
        {
            string pluginFontPath = GetPluginFontPath();
            string userFontPath = GetUserFontPath();

            if (string.IsNullOrEmpty(pluginFontPath) || string.IsNullOrEmpty(userFontPath))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(userFontPath);
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Warning(ex, "Failed to create user font directory.");
            }

            if (!Directory.Exists(userFontPath))
            {
                return;
            }

            string[] pluginFonts;
            try
            {
                pluginFonts = Directory.GetFiles(pluginFontPath).Where(x => x.EndsWith(".ttf") || x.EndsWith(".otf")).ToArray();
            }
            catch
            {
                pluginFonts = [];
            }

            foreach (string fontFileNames in pluginFonts)
            {
                try
                {
                    if (!string.IsNullOrEmpty(fontFileNames))
                    {
                        string fileName = fontFileNames.Replace(pluginFontPath, string.Empty);
                        string copyPath = Path.Combine(userFontPath, fileName);
                        if (!File.Exists(copyPath))
                        {
                            File.Copy(fontFileNames, copyPath, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    IPluginLog.Get().Warning(ex, "Failed to copy font {FontPath} to user font directory.", fontFileNames);
                }
            }
        }

        public static string GetPluginFontPath()
        {
            string? path = Plugin.AssemblyLocation;
            if (path is not null)
            {
                return Path.Join(path, "Media\\Fonts\\");
            }

            return string.Empty;
        }

        public static string GetUserFontPath()
        {
            return Path.Join(Plugin.ConfigFileDir, "\\Fonts\\");
        }

        public static string GetFontName(string? fontPath, string fontFile)
        {
            return fontFile.Replace(fontPath ?? string.Empty, string.Empty)
                .Replace(".otf", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(".ttf", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public static string[] GetFontPaths(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return [];
            }

            string[] fonts;
            try
            {
                fonts = Directory.GetFiles(path).Where(x => x.EndsWith(".ttf") || x.EndsWith(".otf")).ToArray();
            }
            catch
            {
                fonts = [];
            }

            return fonts;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeFontHandles();
            }
        }
    }
}
