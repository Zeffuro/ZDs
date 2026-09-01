using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using ZDs.Config;
using ZDs.Helpers;

namespace ZDs.Windows
{
    public class ConfigWindow : Window
    {
        private bool _back = false;
        private bool _home = false;
        private bool _firstOpen = true;
        private string _name = string.Empty;
        private Vector2 _windowSize;
        private Vector2 _windowPosition;
        private readonly Stack<IConfigurable> _configStack;

        public ConfigWindow(string id, Vector2 size) : base(id)
        {
            this.Flags =
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoScrollWithMouse |
                ImGuiWindowFlags.NoSavedSettings;
            
            this.PositionCondition = ImGuiCond.Appearing;
            this.SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new Vector2(size.X, 160),
                MaximumSize = ImGui.GetMainViewport().Size
            };

            _windowSize = size;
            _configStack = new Stack<IConfigurable>();
        }

        public void PushConfig(IConfigurable configItem)
        {
            _configStack.Push(configItem);
            _name = configItem.Name;
            _firstOpen = true;
            this.IsOpen = true;
        }

        public void ReplaceRootConfig(IConfigurable configItem)
        {
            if (_configStack.Count == 0)
            {
                return;
            }

            _configStack.Clear();
            _configStack.Push(configItem);
            _name = configItem.Name;
        }

        public void ToggleWindow()
        {
            if (IsOpen)
            {
                CloseConfig();
            }
            else
            {
                _configStack.Push(Plugin.Config);
                _firstOpen = true;
            }
            IsOpen = !IsOpen;
        }

        public void CloseForReset()
        {
            _configStack.Clear();
            IsOpen = false;
        }

        public override void PreDraw()
        {
            if (_configStack.Count != 0)
            {
                this.WindowName = string.Join("  >  ", _configStack.Reverse().Select(c => c.Name));

                if (_firstOpen)
                {
                    var viewport = ImGui.GetMainViewport();
                    if (viewport.Size.X > 0 && viewport.Size.Y > 0)
                    {
                        _windowSize = new Vector2(700, 700); // Or your default size
                        _windowPosition = viewport.Pos + viewport.Size / 2f - _windowSize / 2f;

                        ImGui.SetNextWindowSize(_windowSize, ImGuiCond.FirstUseEver);
                        ImGui.SetNextWindowPos(_windowPosition, ImGuiCond.FirstUseEver);
                    }
                }
            }
        }

        public override void Draw()
        {
            if (_configStack.Count == 0)
            {
                Plugin.Config.GeneralConfig.TimelineLocked = true;
                ConfigHelpers.SaveConfig();
                this.IsOpen = false;
                return;
            }

            IConfigurable configItem = _configStack.Peek();
            Vector2 spacing = ImGui.GetStyle().ItemSpacing;
            using var tabBar = ImRaii.TabBar($"##{this.WindowName}");
            if (tabBar)
            {
                foreach (IConfigPage page in configItem.GetConfigPages())
                {
                    using var tabItem = ImRaii.TabItem($"{page.Name}##{this.WindowName}");
                    page.Active = tabItem;
                    if (page.Active)
                    {
                        page.DrawConfig(ImGui.GetContentRegionAvail(), spacing.X, spacing.Y);
                    }
                }
            }

            _windowPosition = ImGui.GetWindowPos();
            _windowSize = ImGui.GetWindowSize();
            
            _firstOpen = false;
        }

        public override void PostDraw()
        {
            if (_home)
            {
                while (_configStack.Count > 1)
                {
                    _configStack.Pop();
                }
            }
            else if (_back)
            {
                _configStack.Pop();
            }

            if ((_home || _back) && _configStack.Count > 1)
            {
                _name = _configStack.Peek().Name;
            }

            _home = false;
            _back = false;
        }

        public override void OnClose()
        {
            CloseConfig();
        }

        private void CloseConfig()
        {
            Plugin.Config.GeneralConfig.Preview = false;
            Plugin.Config.GeneralConfig.TimelineLocked = true;
            ConfigHelpers.SaveConfig();
            _configStack.Clear();
        }
    }
}
