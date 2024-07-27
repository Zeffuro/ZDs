using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using Dalamud.Plugin.Services;
using ImGuiNET;
using ZDs.Config;
using ZDs.Helpers;
using ZDs.Windows;

namespace ZDs
{
    public class Plugin : IDalamudPlugin
    {
        public static IClientState ClientState { get; private set; } = null!;
        public static ICommandManager CommandManager { get; private set; } = null!;
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        public static ICondition Condition { get; private set; } = null!;
        public static IDataManager DataManager { get; private set; } = null!;
        public static IFramework Framework { get; private set; } = null!;
        public static IGameGui GameGui { get; private set; } = null!;
        public static ISigScanner SigScanner { get; private set; } = null!;
        public static IUiBuilder UiBuilder { get; private set; } = null!;
        public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
        public static IKeyState KeyState { get; private set; } = null!;
        public static IPluginLog Logger { get; private set; } = null!;
        public static ITextureProvider TextureProvider { get; private set; } = null!;

        public static string AssemblyLocation { get; private set; } = "";
        public string Name => "ZDs";

        public const string ConfigFileName = "ZDs.json";
        public static string Version { get; private set; } = "";
        public static string ConfigFileDir { get; private set; } = "";
        public static string ConfigFilePath { get; private set; } = "";

        public static Settings Settings { get; private set; } = null!;
        
        public static ZDsConfig Config { get; private set; } = null!;

        private static WindowSystem _windowSystem = null!;
        private static ConfigWindow _configWindow = null!;
        private static TimelineWindow _timelineWindow = null!;
        private Vector2 _origin;


        public Plugin(
            IClientState clientState,
            ICommandManager commandManager,
            IDalamudPluginInterface pluginInterface,
            ICondition condition,
            IDataManager dataManager,
            IFramework framework,
            IGameGui gameGui,
            ISigScanner sigScanner,
            IGameInteropProvider gameInteropProvider,
            
            IKeyState keyState,
            IPluginLog logger,
            ITextureProvider textureProvider
        )
        {
            ClientState = clientState;
            CommandManager = commandManager;
            PluginInterface = pluginInterface;
            Condition = condition;
            DataManager = dataManager;
            Framework = framework;
            GameGui = gameGui;
            SigScanner = sigScanner;
            GameInteropProvider = gameInteropProvider;
            UiBuilder = PluginInterface.UiBuilder;
            KeyState = keyState;
            Logger = logger;
            TextureProvider = textureProvider;

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

            UiBuilder.Draw += Draw;
            UiBuilder.OpenConfigUi += OpenConfigUi;

            CommandManager.AddHandler(
                "/zd",
                new CommandInfo(PluginCommand)
                {
                    HelpMessage = "Opens the ZDs configuration window.",

                    ShowInHelp = true
                }
            );

            TimelineManager.Initialize();

            // Load config
            FontsManager.CopyPluginFontsToUserPath();
            Config = ConfigHelpers.LoadConfig(ConfigFilePath);

            // Refresh fonts
            Config.FontConfig.RefreshFontList();

            // Register config
            Singletons.Register(Config);

            // Initialize Fonts
            Singletons.Register(new FontsManager(pluginInterface.UiBuilder, Config.FontConfig.Fonts.Values));

            CreateWindows();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void PluginCommand(string command, string arguments)
        {
            {
                ShowSettingsWindow();
            }
        }

        private void CreateWindows()
        {
            _origin = ImGui.GetMainViewport().Size / 2f;
            _configWindow = new ConfigWindow("ZDs v"+ Version, _origin, new Vector2(700, 700));
            _timelineWindow = new TimelineWindow("Timeline");

            _windowSystem = new WindowSystem("ZDs_Windows");
            _windowSystem.AddWindow(_configWindow);
            _windowSystem.AddWindow(_timelineWindow);
        }

        private void Draw()
        {
            if (Config == null || ClientState.LocalPlayer == null) return;

            UpdateTimeline();

            _windowSystem?.Draw();
        }

        public static void ShowSettingsWindow()
        {
            if (!_configWindow.IsOpen)
            {
                _configWindow.PushConfig(Config);
            }
        }

        private void UpdateTimeline()
        {
            bool show = Config.GeneralConfig.ShowTimeline;
            if (show)
            {
                if (Config.GeneralConfig.ShowTimelineOnlyInCombat && !Condition[ConditionFlag.InCombat])
                {
                    show = false;
                }

                if (Config.GeneralConfig.ShowTimelineOnlyInDuty && !Condition[ConditionFlag.BoundByDuty])
                {
                    show = false;
                }
            }

            _timelineWindow.IsOpen = show;
        }

        private void OpenConfigUi() => ShowSettingsWindow();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            ConfigHelpers.SaveConfig();

            TimelineManager.Instance?.Dispose();

            _windowSystem.RemoveAllWindows();

            CommandManager.RemoveHandler("/zd");

            UiBuilder.Draw -= Draw;
            UiBuilder.OpenConfigUi -= OpenConfigUi;
            UiBuilder.FontAtlas.BuildFontsAsync();
        }
    }
}
