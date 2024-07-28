using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ZDs.Config;
using ZDs.Helpers;

namespace ZDs.Windows
{
    public class ConfigWindow : Window
    {
        private const float NavBarHeight = 40;

        private bool _back = false;
        private bool _home = false;
        private string _name = string.Empty;
        private Vector2 _windowSize;
        private readonly Stack<IConfigurable> _configStack;

        public ConfigWindow(string id, Vector2 position, Vector2 size) : base(id)
        {
            this.Flags =
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoScrollWithMouse |
                ImGuiWindowFlags.NoSavedSettings;

            this.Position = position - size / 2;
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
            this.IsOpen = true;
        }

        public void ToggleWindow()
        {
            if (IsOpen)
            {
                _configStack.Clear();
            }
            else
            {
                _configStack.Push(Plugin.Config);
            }
            IsOpen = !IsOpen;
        }

        public override void PreDraw()
        {
            if (_configStack.Count != 0)
            {
                this.WindowName = string.Join("  >  ", _configStack.Reverse().Select(c => c.Name));
                ImGui.SetNextWindowSize(_windowSize);
            }
        }

        public override void Draw()
        {
            if (_configStack.Count == 0)
            {
                this.IsOpen = false;
                return;
            }

            IConfigurable configItem = _configStack.Peek();
            Vector2 spacing = ImGui.GetStyle().ItemSpacing;
            Vector2 size = _windowSize - spacing * 2;

            size -= new Vector2(0, NavBarHeight + spacing.Y);
            

            IConfigPage? openPage = null;
            if (ImGui.BeginTabBar($"##{this.WindowName}"))
            {
                foreach (IConfigPage page in configItem.GetConfigPages())
                {
                    page.Active = ImGui.BeginTabItem($"{page.Name}##{this.WindowName}");
                    if (page.Active)
                    {
                        openPage = page;
                        page.DrawConfig(size.AddY(-ImGui.GetCursorPosY()), spacing.X, spacing.Y);
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }

            this.Position = ImGui.GetWindowPos();
            _windowSize = ImGui.GetWindowSize();
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
            Plugin.Config.GeneralConfig.Preview = false;
            ConfigHelpers.SaveConfig();
            _configStack.Clear();

            ZDsConfig config = Singletons.Get<ZDsConfig>();
        }
    }
}