using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using ZDs.Config;
using ZDs.Helpers;
using ZDs.Timeline;
using ZDs.Windows;

namespace ZDs
{
    public sealed class Plugin : IAsyncDalamudPlugin
    {
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        public static string AssemblyLocation { get; private set; } = "";
        public string Name => "ZDs";

        public const string ConfigFileName = "ZDs.json";
        public static string Version { get; private set; } = "";
        public static string ConfigFileDir { get; private set; } = "";
        public static string ConfigFilePath { get; private set; } = "";
        public static string Changelog { get; private set; } = string.Empty;

        public static ZDsConfig Config { get; set; } = null!;

        private static WindowSystem? _windowSystem;
        private static ConfigWindow? _configWindow;
        private static TimelineWindow? _timelineWindow;
        private static TimelineSetupWindow? _setupWindow;
        private bool _loadStarted;
        private bool _configLoaded;
        private bool _commandRegistered;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;

            if (pluginInterface.AssemblyLocation.DirectoryName != null)
            {
                AssemblyLocation = pluginInterface.AssemblyLocation.DirectoryName + "\\";
            }
            else
            {
                AssemblyLocation = Assembly.GetExecutingAssembly().Location;
            }

            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
            ConfigFileDir = pluginInterface.GetPluginConfigDirectory();
            ConfigFilePath = Path.Combine(pluginInterface.GetPluginConfigDirectory(), Plugin.ConfigFileName);
        }

        public Task LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _loadStarted = true;

            PluginInterface.UiBuilder.Draw += Draw;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            ICommandManager.Get().AddHandler(
                "/zd",
                new CommandInfo(PluginCommand)
                {
                    HelpMessage = "Opens settings. Use '/zd profile <name>' to switch profiles or '/zd profile save <name>' to save one.",
                    ShowInHelp = true
                }
            );
            _commandRegistered = true;

            Singletons.Register(new ClipRectsHelper());

            // Load changelog
            Changelog = LoadChangelog();

            // Load config
            FontsManager.CopyPluginFontsToUserPath();
            ConfigLoadResult configLoad = ConfigHelpers.LoadConfig(ConfigFilePath);
            Config = configLoad.Config;
            _configLoaded = true;

            // Disable preview
            Config.GeneralConfig.Preview = false;

            // Refresh fonts
            Config.FontConfig.RefreshFontList();
            Config.FontConfig.NormalizeFonts();

            // Register config
            Singletons.Register(Config);

            // Initialize Fonts
            Singletons.Register(new FontsManager(PluginInterface.UiBuilder, Config.FontConfig.Fonts.Values));

            ProfileManager.Initialize(configLoad.IsNewInstall);

            // Timeline callbacks may run immediately after subscription, so initialize only after config is ready.
            TimelineManager.Initialize();

            CreateWindows();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_loadStarted)
            {
                return ValueTask.CompletedTask;
            }

            _loadStarted = false;
            if (_configLoaded)
            {
                ProfileManager.SaveActive();
                ConfigHelpers.SaveConfig();
            }

            TimelineManager.Instance?.Dispose();
            _windowSystem?.RemoveAllWindows();

            if (_commandRegistered)
            {
                ICommandManager.Get().RemoveHandler("/zd");
                _commandRegistered = false;
            }

            PluginInterface.UiBuilder.Draw -= Draw;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

            if (Singletons.IsRegistered<FontsManager>())
            {
                Singletons.Get<FontsManager>().Dispose();
            }

            _configLoaded = false;

            return ValueTask.CompletedTask;
        }
        private void PluginCommand(string command, string arguments)
        {
            string input = arguments.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ToggleSettingsWindow();
                return;
            }

            if (input.Equals("profiles", StringComparison.OrdinalIgnoreCase))
            {
                DrawHelper.DrawNotification($"Profiles: {string.Join(", ", ProfileManager.ProfileNames)}");
                return;
            }

            string[] commandParts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (commandParts.Length == 0 || !commandParts[0].Equals("profile", StringComparison.OrdinalIgnoreCase))
            {
                DrawHelper.DrawNotification("Usage: /zd profile <name> or /zd profile save <name>", NotificationType.Warning);
                return;
            }

            string profileArgument = commandParts.Length == 2 ? commandParts[1] : string.Empty;
            string[] profileParts = profileArgument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (profileParts.Length > 0 && profileParts[0].Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                string profileName = profileParts.Length == 2 ? profileParts[1] : string.Empty;
                bool saved = !string.IsNullOrEmpty(profileName) && ProfileManager.SaveCurrent(profileName);
                DrawHelper.DrawNotification(
                    saved ? $"Saved profile '{profileName}'." : "Usage: /zd profile save <name>",
                    saved ? NotificationType.Success : NotificationType.Warning);
                return;
            }

            bool switched = !string.IsNullOrEmpty(profileArgument) && ProfileManager.Switch(profileArgument);
            DrawHelper.DrawNotification(
                switched ? $"Switched to profile '{ProfileManager.ActiveProfile}'." : $"Profile '{profileArgument}' was not found.",
                switched ? NotificationType.Success : NotificationType.Error);
        }

        public static void ApplyConfig(ZDsConfig config, bool save = true)
        {
            ConfigMigration.Migrate(config);
            config.GeneralConfig.Preview = false;
            config.FontConfig.RefreshFontList();
            config.FontConfig.NormalizeFonts();
            Config = config;
            Singletons.Update(config);
            _configWindow?.ReplaceRootConfig(config);

            if (Singletons.IsRegistered<FontsManager>())
            {
                Singletons.Get<FontsManager>().UpdateFonts(config.FontConfig.Fonts.Values);
            }

            if (save)
            {
                ConfigHelpers.SaveConfig(config);
            }
        }

        private void CreateWindows()
        {
            _configWindow = new ConfigWindow($"ZDs v{Version}", new Vector2(700, 700));
            _timelineWindow = new TimelineWindow("ZDS_Timeline");
            _setupWindow = new TimelineSetupWindow();

            _windowSystem = new WindowSystem("ZDs_Windows");
            _windowSystem.AddWindow(_configWindow);
            _windowSystem.AddWindow(_timelineWindow);
            _windowSystem.AddWindow(_setupWindow);
        }

        private void Draw()
        {
            if (Config == null) return;

            if (IObjectTable.Get().LocalPlayer != null)
            {
                UpdateTimeline();
            }
            else if (_timelineWindow != null)
            {
                _timelineWindow.IsOpen = false;
            }

            Singletons.Get<ClipRectsHelper>().Update();

            _windowSystem?.Draw();
        }

        public static void ToggleSettingsWindow()
        {
            _configWindow?.ToggleWindow();
        }

        public static void ShowSetupAfterReset()
        {
            _configWindow?.CloseForReset();
            _setupWindow?.OpenForCurrentConfig();
        }

        private void UpdateTimeline()
        {
            bool show = Config.GeneralConfig.ShowTimeline;

            if (show)
            {
                if (Config.GeneralConfig.ShowTimelineOnlyInCombat && !ICondition.Get()[ConditionFlag.InCombat])
                {
                    show = false;
                }

                if (Config.GeneralConfig.ShowTimelineOnlyInDuty && !ICondition.Get()[ConditionFlag.BoundByDuty])
                {
                    show = false;
                }
            }

            if(_timelineWindow != null ) _timelineWindow.IsOpen = show;
        }

        private void OpenConfigUi() => ToggleSettingsWindow();

        private static string LoadChangelog()
        {
            if (string.IsNullOrEmpty(AssemblyLocation))
            {
                return string.Empty;
            }

            string changelogPath = Path.Combine(AssemblyLocation, "changelog.md");

            if (File.Exists(changelogPath))
            {
                try
                {
                    string changelog = File.ReadAllText(changelogPath);
                    return changelog.Replace("# ", string.Empty);
                }
                catch (Exception ex)
                {
                    IPluginLog.Get().Warning(ex, "Error loading changelog.");
                }
            }

            return string.Empty;
        }

    }
}
