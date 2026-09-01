using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Network;
using ZDs.Config;
using LuminaAction = Lumina.Excel.Sheets.Action;
using ZDs.Helpers;

namespace ZDs.Timeline
{
    public partial class TimelineManager
    {
        private const int MaxItemCount = 50;
        private const uint FadeoutDirectValue = 0x4000000F;
        private const double SourceUseRetrySeconds = 1.0;

        private readonly List<TimelineItem> _items = new List<TimelineItem>(MaxItemCount);
        private readonly List<TimelineItem> _completedItems = new List<TimelineItem>(MaxItemCount);
        private readonly ExcelSheet<LuminaAction> _sheet;
        private readonly Dictionary<TimelineKey, PendingSourceUse> _pendingSourceUses = [];

        private Hook<ActionEffectHandler.Delegates.Receive>? _onActionUsedHook;

        private Hook<PacketDispatcher.Delegates.HandleActorControlPacket>? _onActorControlHook;
        private Hook<PacketDispatcher.Delegates.HandleActorCastPacket>? _onActorCastHook;
        private bool _hadSwiftcast = false;
        private bool _hadMudra = false;

        private bool _isPreviewActive = false;

        private bool _disposed = false;

        public static void Initialize() { Instance = new TimelineManager(); }

        public static TimelineManager Instance { get; private set; } = null!;
        public IReadOnlyCollection<TimelineItem> Items => _items.Concat(_completedItems).ToList().AsReadOnly();
        private ZDsConfig Config => Plugin.Config;

        public unsafe TimelineManager()
        {
            _sheet = IDataManager.Get().GetExcelSheet<LuminaAction>();

            try
            {
                _onActionUsedHook = IGameInteropProvider.Get().HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                    ActionEffectHandler.MemberFunctionPointers.Receive,
                    OnActionUsed
                );
                _onActionUsedHook?.Enable();

                _onActorControlHook = IGameInteropProvider.Get().HookFromAddress<PacketDispatcher.Delegates.HandleActorControlPacket>(
                    PacketDispatcher.MemberFunctionPointers.HandleActorControlPacket,
                    OnActorControl
                );
                _onActorControlHook?.Enable();

                _onActorCastHook = IGameInteropProvider.Get().HookFromAddress<PacketDispatcher.Delegates.HandleActorCastPacket>(
                    PacketDispatcher.MemberFunctionPointers.HandleActorCastPacket,
                    OnCast
                );
                _onActorCastHook?.Enable();
            }
            catch (Exception e)
            {
                IPluginLog.Get().Error(e, "Error initiating hooks.");
            }

            IFramework.Get().Update += Update;
        }

        ~TimelineManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (!disposing)
            {
                return;
            }

            IFramework.Get().Update -= Update;

            _items.Clear();

            _onActionUsedHook?.Disable();
            _onActionUsedHook?.Dispose();
            _onActionUsedHook = null;

            _onActorControlHook?.Disable();
            _onActorControlHook?.Dispose();
            _onActorControlHook = null;

            _onActorCastHook?.Disable();
            _onActorCastHook?.Dispose();
            _onActorCastHook = null;
        }


        private void Update(IFramework framework)
        {
            CheckPreview();
            CheckStatuses();
            SynchronizeOptionalSources();
            CheckCooldown();
        }

        private void CheckPreview()
        {
            if ((Config?.GeneralConfig?.Preview ?? false) == _isPreviewActive)
                return;

            _isPreviewActive = !_isPreviewActive;

            if (_isPreviewActive)
            {
                LoadPreview();
            }
            else
            {
                ResetCooldowns();
            }
        }

        private void CheckCooldown()
        {
            ZDsConfig? config = Plugin.Config;
            if (config?.CooldownConfig is not CooldownConfig cooldownConfig)
            {
                return;
            }

            float finishDuration = cooldownConfig.FinishAnimation == CooldownFinishAnimation.None
                ? 0f
                : cooldownConfig.FinishAnimationDuration;
            float readyDuration = Config.LayoutConfig.ReadyLandingEnabled
                ? Config.LayoutConfig.ReadyLandingDuration
                : 0f;
            float completionDuration = Math.Max(finishDuration, readyDuration);
            if (completionDuration <= 0)
            {
                _completedItems.Clear();
            }
            else
            {
                double now = ImGui.GetTime();
                _completedItems.RemoveAll(item => now - item.CompletionTime.GetValueOrDefault() > completionDuration);
            }

            var currentTime = ImGui.GetTime();
            SynchronizeChargedCooldowns(currentTime, cooldownConfig);

            foreach (var item in _items.ToList())
            {

                if (currentTime - item.Time <= item.Cooldown / 10)
                    continue;

                if (completionDuration > 0)
                {
                    if (_completedItems.Count >= MaxItemCount)
                    {
                        _completedItems.RemoveAt(0);
                    }

                    _completedItems.Add(item.CreateCompletion(currentTime));
                }

                if (item.MaxCharges > 1 && item.ActiveCharges < item.MaxCharges)
                {
                    continue;
                }

                _items.Remove(item);
            }
        }

        private bool ShouldRetainCompletion(CooldownConfig cooldownConfig) =>
            (cooldownConfig.FinishAnimation != CooldownFinishAnimation.None && cooldownConfig.FinishAnimationDuration > 0) ||
            (Config.LayoutConfig.ReadyLandingEnabled && Config.LayoutConfig.ReadyLandingDuration > 0);

        private void LoadPreview()
        {
            foreach (var (actionId, type) in TimelineActionCatalog.PreviewActions)
            {
                AddItem(actionId, type, true);
            }
        }

        private void SortTimeline()
        {
            _items.Sort((x, y) => x.Cooldown.CompareTo(y.Cooldown));
        }

        private void ResetCooldowns()
        {
            _items.Clear();
            _completedItems.Clear();
            _pendingSourceUses.Clear();
            _statusDurations.Clear();
        }

        private void SynchronizeOptionalSources()
        {
            double now = ImGui.GetTime();

            if (!Config.SourcesConfig.TrackItems)
            {
                RemoveSource(TimelineSourceKind.Item);
            }

            if (!Config.SourcesConfig.TrackDutyActions)
            {
                RemoveSource(TimelineSourceKind.DutyAction);
            }

            if (!Config.SourcesConfig.TrackLostActions)
            {
                RemoveSource(TimelineSourceKind.LostAction);
            }

            if (Config.SourcesConfig.TrackDutyActions || Config.SourcesConfig.TrackLostActions)
            {
                SynchronizeDutyActions(now);
            }

            if (!Config.SourcesConfig.TrackPhantomActions)
            {
                RemoveSource(TimelineSourceKind.PhantomAction);
            }
            else
            {
                SynchronizePhantomActions(now);
            }

            if (!Config.SourcesConfig.TrackDotsAndDebuffs && !Config.SourcesConfig.TrackBuffs)
            {
                RemoveSource(TimelineSourceKind.Status);
                _statusDurations.Clear();
                EnemyListHelper.Clear();
            }
            else
            {
                SynchronizeStatuses(now);
            }

            foreach (PendingSourceUse pending in _pendingSourceUses.Values.ToList())
            {
                bool enabled = IsSourceEnabled(pending.Key.SourceKind);

                if (!enabled || now > pending.Deadline)
                {
                    _pendingSourceUses.Remove(pending.Key);
                    continue;
                }

                bool added = pending.Key.SourceKind switch
                {
                    TimelineSourceKind.Item => TryAddUsedItem(pending.NativeId, now),
                    TimelineSourceKind.DutyAction or TimelineSourceKind.LostAction =>
                        TryAddDutyAction(pending.Key.Id, pending.Key.SourceKind, now),
                    TimelineSourceKind.PhantomAction => TryAddPhantomAction(pending.Key.Id, now),
                    _ => false,
                };

                if (added)
                {
                    _pendingSourceUses.Remove(pending.Key);
                }
            }
        }

        private bool IsSourceEnabled(TimelineSourceKind sourceKind) => sourceKind switch
        {
            TimelineSourceKind.Item => Config.SourcesConfig.TrackItems,
            TimelineSourceKind.DutyAction => Config.SourcesConfig.TrackDutyActions,
            TimelineSourceKind.LostAction => Config.SourcesConfig.TrackLostActions,
            TimelineSourceKind.PhantomAction => Config.SourcesConfig.TrackPhantomActions,
            TimelineSourceKind.Status => Config.SourcesConfig.TrackDotsAndDebuffs || Config.SourcesConfig.TrackBuffs,
            _ => true,
        };

        private void RemoveSource(TimelineSourceKind sourceKind)
        {
            _items.RemoveAll(item => item.Key.SourceKind == sourceKind);
            _completedItems.RemoveAll(item => item.Key.SourceKind == sourceKind);
            foreach (TimelineKey key in _pendingSourceUses.Keys.Where(key => key.SourceKind == sourceKind).ToList())
            {
                _pendingSourceUses.Remove(key);
            }
        }

        private void CompleteAndRemove(TimelineItem item, double now)
        {
            if (ShouldRetainCompletion(Config.CooldownConfig))
            {
                if (_completedItems.Count >= MaxItemCount)
                {
                    _completedItems.RemoveAt(0);
                }

                _completedItems.Add(item.CreateCompletion(now));
            }

            _items.Remove(item);
        }

        private void EnsureItemCapacity()
        {
            if (_items.Count >= MaxItemCount)
            {
                _items.RemoveAt(0);
            }
        }

        private readonly record struct PendingSourceUse(TimelineKey Key, uint NativeId, double Deadline);

    }
}
