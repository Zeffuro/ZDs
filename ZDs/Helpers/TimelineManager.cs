using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using ZDs.Config;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ZDs.Helpers
{
    public enum TimelineItemType
    {
        Action = 0,
        CastStart = 1,
        CastCancel = 2,
        OffGCD = 3,
        AutoAttack = 4
    }

    public class TimelineItem
    {
        public uint ActionID { get; }
        public uint IconID { get; }
        public TimelineItemType Type { get; }
        public double Time { get; set; }

        public float GCDDuration { get; }
        public float CastTime { get; }
        public float Cooldown { get; }
        public int MaxCharges { get; }
        public int ActiveCharges { get; set; }

        public TimelineItem(uint actionID, uint iconID, TimelineItemType type, double time)
        {
            ActionID = actionID;
            IconID = iconID;
            Type = type;
            Time = time;
            GCDDuration = 0;
            CastTime = 0;
        }

        public TimelineItem(uint actionID, uint iconID, TimelineItemType type, double time, float gcdDuration, float castTime, float cooldown, int maxCharges) : this(actionID, iconID, type, time)
        {
            GCDDuration = gcdDuration;
            CastTime = castTime;
            Cooldown = cooldown;
            MaxCharges = maxCharges;
            ActiveCharges = maxCharges;
        }

        public override string ToString() =>
            $"ActionID={ActionID}, IconID={IconID}, Type={Type}, Time={Time:F2}, " +
            $"GCDDuration={GCDDuration:F2}, CastTime={CastTime:F2}, Cooldown={Cooldown:F2}, " +
            $"MaxCharges={MaxCharges}, ActiveCharges={ActiveCharges}";
    }

    public class TimelineManager
    {
        #region singleton
        public static void Initialize() { Instance = new TimelineManager(); }

        public static TimelineManager Instance { get; private set; } = null!;

        public unsafe TimelineManager()
        {
            _sheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();

            try
            {
                _onActionUsedHook = Plugin.GameInteropProvider.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                    ActionEffectHandler.MemberFunctionPointers.Receive,
                    OnActionUsed
                );
                _onActionUsedHook?.Enable();

                _onActorControlHook = Plugin.GameInteropProvider.HookFromSignature<OnActorControlDelegate>(
                    "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64",
                    OnActorControl
                );
                _onActorControlHook?.Enable();

                _onCastHook = Plugin.GameInteropProvider.HookFromSignature<OnCastDelegate>(
                    "40 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B F2",
                    OnCast
                );
                _onCastHook?.Enable();
            }
            catch (Exception e)
            {
                Plugin.Logger.Error("Error initiating hooks: " + e.Message);
            }

            Plugin.Framework.Update += Update;
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

        protected void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            Plugin.Framework.Update -= Update;

            _items.Clear();

            _onActionUsedHook?.Disable();
            _onActionUsedHook?.Dispose();

            _onActorControlHook?.Disable();
            _onActorControlHook?.Dispose();

            _onCastHook?.Disable();
            _onCastHook?.Dispose();
        }
        #endregion

        private delegate void OnActionUsedDelegate(uint sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private Hook<ActionEffectHandler.Delegates.Receive>? _onActionUsedHook;

        private delegate void OnActorControlDelegate(uint entityId, uint id, uint unk1, uint type, uint unk2, uint unk3, uint unk4, uint unk5, UInt64 targetId, byte unk6);
        private Hook<OnActorControlDelegate>? _onActorControlHook;

        private delegate void OnCastDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<OnCastDelegate>? _onCastHook;

        private ExcelSheet<LuminaAction> _sheet;
        private Dictionary<uint, uint> _specialCasesMap = new()
        {
            // BRD
            [110] = 110,    // bloodletter
            [117] = 110,    // rain of death
            [36975] = 110,  // bloodletter
            
            // MCH
            [16498] = 16498, // drill
            [16499] = 16498, // bio blaster
            
            // MNK
            [16475] = 53,   // anatman

            // NIN
            [2259] = 2259,   // ten
            [2261] = 2259,   // chi
            [2263] = 2259,   // jin
            [18805] = 18805, // ten
            [18806] = 18805, // chi
            [18807] = 18805, // jin
            
            // SAM
            [16484] = 7477, // kaeshi higanbana
            [16485] = 7477, // kaeshi goken
            [16486] = 7477, // keashi setsugekka

            // RDM
            [25858] = 7504,  // resolution
            [16527] = 16527, // engagement
            [7515] = 16527, // displacement
            
            // RPR
            [24380] = 24380, // soul slice
            [24381] = 24380, // soul scythe
            
            // VPR
            [34620] = 34620, // vicewinder
            [34623] = 34620, // vice Pit
            
        };
        private Dictionary<uint, float> _hardcodedCasesMap = new()
        {
            // NIN
            [2259] = 0.5f, // ten
            [2261] = 0.5f, // chi
            [2263] = 0.5f, // jin
            [18805] = 0.5f, // ten
            [18806] = 0.5f, // chi
            [18807] = 0.5f, // jin
            [2265] = 1.5f, // fuma shuriken
            [2266] = 1.5f, // katon
            [2267] = 1.5f, // raiton
            [2268] = 1.5f, // hyoton
            [2269] = 1.5f, // huton
            [2270] = 1.5f, // doton
            [2271] = 1.5f, // suiton
            [2272] = 1.5f, // rabbit medium
            [16491] = 1.5f, // goka mekkyaku
            [16492] = 1.5f, // hyosho ranryu
        };
        
        private Dictionary<uint, uint> _hardcodedIconIds = new()
        {
            // General
            [1] = 101,
            [3] = 104,
            
            // NIN
            [2259] = 2904,  // ten
            [2261] = 2904,  // chi
            [2263] = 2904,  // jin
            [18805] = 2904, // ten
            [18806] = 2904, // chi
            [18807] = 2904, // jin
        };
        
        private static readonly (uint ActionId, TimelineItemType Type)[] _previewScript =
        [
            (110, TimelineItemType.OffGCD),
            (2874, TimelineItemType.OffGCD),
            (7561, TimelineItemType.OffGCD),
            (7562, TimelineItemType.OffGCD),
            (7531, TimelineItemType.OffGCD),
            (34675, TimelineItemType.OffGCD),
            (43, TimelineItemType.OffGCD),
        ];

        private static int kMaxItemCount = 50;
        private List<TimelineItem> _items = new List<TimelineItem>(kMaxItemCount);
        public IReadOnlyCollection<TimelineItem> Items => _items.AsReadOnly();

        private ZDsConfig Config => Plugin.Config;

        private double _outOfCombatStartTime = -1;
        private bool _hadSwiftcast = false;
        private bool _hadMudra = false;
        private bool _hadMB = false;
        
        private bool _isPreviewActive = false;


        private unsafe void Update(IFramework framework)
        {
            CheckPreview();
            CheckStatuses();
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

        private void CheckStatuses()
        {
            IPlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player != null)
            {
                _hadSwiftcast = player.StatusList.Any(s => s.StatusId == 167);
                _hadMudra = player.StatusList.Any(s => s.StatusId == 496);
                _hadMB = player.StatusList.Any(s => s.StatusId == 2217 && s.SourceId == player.GameObjectId);
            }
        }
        
        private void CheckCooldown()
        {
            if (_hadMB)
            {
                // Handle Bloodletter and their upgraded counterparts
                HandleSpecialChargedActions([110]);
            }
            
            foreach (var item in _items.ToList().Where(item => ImGui.GetTime() - item.Time > item.Cooldown / 10))
            {
                if (item.MaxCharges > 0 && item.ActiveCharges != item.MaxCharges)
                {
                    var foundItem = _items.Find(x => x == item);
                    foundItem!.ActiveCharges++;
                    foundItem!.Time = ImGui.GetTime();

                    return;
                }
                _items.Remove(item);
            }
        }

        private void HandleSpecialChargedActions(uint[] actionIds)
        {
            ActionsHelper actionHelper = new ActionsHelper();
            
            foreach (var actionId in actionIds)
            {
                var timelineItem = _items.FirstOrDefault(item => item.ActionID == actionId);
                if (timelineItem != null)
                {
                    actionHelper.GetAdjustedRecastInfo(actionId, out ActionsHelper.RecastInfo recastInfo);
            
                    int stacks = recastInfo.RecastTime == 0f
                        ? recastInfo.MaxCharges
                        : (int)(recastInfo.MaxCharges * (recastInfo.RecastTimeElapsed / recastInfo.RecastTime));

                    float chargeTime = recastInfo.MaxCharges != 0
                        ? recastInfo.RecastTime / recastInfo.MaxCharges
                        : recastInfo.RecastTime;

                    float cooldown = (chargeTime != 0
                        ? Math.Abs(recastInfo.RecastTime - recastInfo.RecastTimeElapsed) % chargeTime
                        : 0);
                        
                    float cooldownLeft = Math.Abs(chargeTime - cooldown);

                    timelineItem.ActiveCharges = stacks;
                    timelineItem.Time = ImGui.GetTime() - cooldownLeft;
                }
            }
        }

        private unsafe float GetGCDTime(uint actionId)
        {
            ActionManager* actionManager = ActionManager.Instance();
            uint adjustedId = actionManager->GetAdjustedActionId(actionId);
            return actionManager->GetRecastTime(ActionType.Action, adjustedId);
        }

        private unsafe float GetCastTime(uint actionId)
        {
            ActionManager* actionManager = ActionManager.Instance();
            uint adjustedId = actionManager->GetAdjustedActionId(actionId);
            return (float)ActionManager.GetAdjustedCastTime(ActionType.Action, adjustedId) / 1000f;
        }

        private void AddItem(uint actionId, TimelineItemType type, bool isPreviewItem = false)
        {
            if (!_sheet.TryGetRow(actionId, out LuminaAction action))
            {
                return;
            }

            ActionsHelper actionHelper = new ActionsHelper();
            actionHelper.GetAdjustedRecastInfo(actionId, out ActionsHelper.RecastInfo recastInfo);
            
            int stacks = recastInfo.RecastTime == 0f
                ? recastInfo.MaxCharges
                : (int)(recastInfo.MaxCharges * (recastInfo.RecastTimeElapsed / recastInfo.RecastTime));

            float chargeTime = recastInfo.MaxCharges != 0
                ? recastInfo.RecastTime / recastInfo.MaxCharges
                : recastInfo.RecastTime;

            float cooldown = (chargeTime != 0
                ? Math.Abs(recastInfo.RecastTime - recastInfo.RecastTimeElapsed) % chargeTime
                : 0) * 10;

            if (isPreviewItem)
            {
                cooldown = action.Recast100ms;
            }

            // only cache the last kMaxItemCount items
            if (_items.Count >= kMaxItemCount)
            {
                _items.RemoveAt(0);
            }

            double now = ImGui.GetTime();
            float gcdDuration = 0;
            float castTime = 0;
            
            if (actionId is 7410 or 36978)
            {
                // Handle Gauss Round / Ricochet and their upgraded counterparts
                HandleSpecialChargedActions([2874, 2890]);
            }

            if (Config.AbilitiesConfig.FilterAbilities)
            {
                bool isFiltered = Config.AbilitiesConfig.FilteredAbilities.ContainsValue(actionId);

                if ((Config.AbilitiesConfig.FilterMode == 0 && isFiltered) || (Config.AbilitiesConfig.FilterMode == 1 && !isFiltered))
                {
                    return; // Skip processing based on filter mode and list presence
                }
            }

            if (Config.AbilitiesConfig.IgnoreAbilitiesBelow && cooldown / 10 <= Config.AbilitiesConfig.IgnoreAbilitiesRecast)
            {
                return;
            }
            
            // handle weird cases
            if (_specialCasesMap.TryGetValue(actionId, out uint replacedId))
            {
                type = TimelineItemType.Action;
                actionId = replacedId;
            }
            
            // Ninja's Hide restores Mudra Charges
            uint[] mudraActionIds = [2259, 2261, 2263];
            if (actionId == 2245)
            {
                _items.RemoveAll(item => mudraActionIds.Contains(item.ActionID));
            }

            if (mudraActionIds.Contains(actionId) && _hadMudra)
            {
                return;
            }

            if (recastInfo.MaxCharges > 0 && _items.Any(x => x.ActionID == actionId))
            {
                _items.Find(x => x.ActionID == actionId)!.ActiveCharges--;
                return;
            }
            
            // handle changed icons
            int iconId = _hardcodedIconIds.TryGetValue(actionId, out uint hardcodedIconId) ? (int)hardcodedIconId : action.Icon;
            
            // calculate gcd and cast time
            if (type == TimelineItemType.CastStart)
            {
                gcdDuration = GetGCDTime(actionId);
                castTime = GetCastTime(actionId);
            }
            else if (type == TimelineItemType.Action)
            {
                TimelineItem? lastItem = _items.LastOrDefault();
                if (lastItem != null && lastItem.Type == TimelineItemType.CastStart)
                {
                    gcdDuration = lastItem.GCDDuration;
                    castTime = lastItem.CastTime;
                }
                else
                {
                    gcdDuration = GetGCDTime(actionId);
                    castTime = _hadSwiftcast ? 0 : GetCastTime(actionId);
                }
            }

            // handle more weird cases
            if (_hardcodedCasesMap.TryGetValue(actionId, out float gcd))
            {
                type = TimelineItemType.Action;
                gcdDuration = gcd;
            }

            TimelineItem item = new TimelineItem(actionId, (uint)iconId, type, now, gcdDuration, castTime, cooldown, stacks);
            _items.Add(item);
            SortTimeline();
        }
        
        private void LoadPreview()
        {
            foreach (var (actionId, type) in _previewScript)
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
        }

        private TimelineItemType? TypeForActionID(uint id)
        {
            LuminaAction? action = _sheet.GetRowOrDefault(id);

            if (action == null)
            {
                return null;
            }

            // off gcd or sprint
            if (action.Value.ActionCategory.RowId is 4 || id == 3)
            {
                return TimelineItemType.OffGCD;
            }

            if (action.Value.ActionCategory.RowId is 1)
            {
                return TimelineItemType.AutoAttack;
            }

            return TimelineItemType.Action;
        }
        
        private unsafe void OnActionUsed(uint actorId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds)
        {
            _onActionUsedHook?.Original(actorId, casterPtr, targetPos, header, effects, targetEntityIds);

            IPlayerCharacter ? player = Plugin.ClientState.LocalPlayer;
            if (player == null || actorId != player.GameObjectId) { return; }

            uint actionId = header->ActionId;
            TimelineItemType? type = TypeForActionID(actionId);
            if (header->ActionType != ActionType.Action || !type.HasValue) { return; }

            AddItem (actionId, type.Value);
        }

        private void OnActorControl(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            _onActorControlHook?.Original(entityId, type, buffID, direct, actionId, sourceId, arg4, arg5, targetId, a10);
            
            // Works for most wipes (it's the fadeout).
            if (direct == 0x4000000F)
            {
                ResetCooldowns();
            }

            if (type != 15) { return; }

            IPlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player == null || entityId != player.GameObjectId) { return; }

            AddItem(actionId, TimelineItemType.CastCancel);
        }

        private void OnCast(uint sourceId, IntPtr ptr)
        {
            _onCastHook?.Original(sourceId, ptr);

            IPlayerCharacter? player = Plugin.ClientState.LocalPlayer;
            if (player == null || sourceId != player.GameObjectId) { return; }

            int value = Marshal.ReadInt16(ptr);
            uint actionId = value < 0 ? (uint)(value + 65536) : (uint)value;
            AddItem(actionId, TimelineItemType.CastStart);
        }
    }
}
