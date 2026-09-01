using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ZDs.Config;
using ZDs.Helpers;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ZDs.Timeline
{
    public partial class TimelineManager
    {
        private void CheckStatuses()
        {
            IPlayerCharacter? player = IObjectTable.Get().LocalPlayer;

            if (player == null)
                return;

            _hadSwiftcast = player.StatusList.Any(s => s.StatusId == 167);
            _hadMudra = player.StatusList.Any(s => s.StatusId == 496);
        }

        private void SynchronizeChargedCooldowns(double now, CooldownConfig cooldownConfig)
        {
            if (_isPreviewActive)
            {
                return;
            }

            ActionsHelper actionHelper = new ActionsHelper();

            foreach (TimelineItem timelineItem in _items.Where(item =>
                         item.Key.SourceKind == TimelineSourceKind.Action && item.MaxCharges > 1).ToList())
            {
                ActionsHelper.RecastInfo recastInfo = actionHelper.GetAdjustedRecastInfo(timelineItem.ActionID);
                if (recastInfo.MaxCharges <= 1 || recastInfo.ChargeTime <= 0)
                {
                    continue;
                }

                int previousCharges = timelineItem.ActiveCharges;
                int maxCharges = recastInfo.MaxCharges;
                int currentCharges = Math.Clamp((int)recastInfo.CurrentCharges, 0, maxCharges);

                timelineItem.MaxCharges = maxCharges;
                timelineItem.ActiveCharges = currentCharges;
                timelineItem.Cooldown = recastInfo.ChargeTime * 10;

                if (currentCharges >= maxCharges)
                {
                    if (ShouldRetainCompletion(cooldownConfig))
                    {
                        if (_completedItems.Count >= MaxItemCount)
                        {
                            _completedItems.RemoveAt(0);
                        }

                        _completedItems.Add(timelineItem.CreateCompletion(now));
                    }

                    _items.Remove(timelineItem);
                    continue;
                }

                float elapsedIntoNextCharge = Math.Clamp(
                    recastInfo.RecastTimeElapsed - currentCharges * recastInfo.ChargeTime,
                    0,
                    recastInfo.ChargeTime);
                timelineItem.Time = now - elapsedIntoNextCharge;

                if (currentCharges > previousCharges &&
                    currentCharges < maxCharges &&
                    ShouldRetainCompletion(cooldownConfig))
                {
                    if (_completedItems.Count >= MaxItemCount)
                    {
                        _completedItems.RemoveAt(0);
                    }

                    _completedItems.Add(timelineItem.CreateCompletion(now));
                }
            }
        }

        private void AddItem(uint actionId, TimelineItemType type, bool isPreviewItem = false)
        {
            if (!_sheet.TryGetRow(actionId, out LuminaAction action))
            {
                return;
            }

            ActionsHelper actionHelper = new ActionsHelper();
            ActionsHelper.RecastInfo recastInfo = actionHelper.GetAdjustedRecastInfo(actionId);
            int maxCharges = Math.Max(1, (int)recastInfo.MaxCharges);
            int stacks = Math.Clamp((int)recastInfo.CurrentCharges, 0, maxCharges);
            float chargeTime = recastInfo.ChargeTime > 0
                ? recastInfo.ChargeTime
                : action.Recast100ms / 10f;
            float cooldown = chargeTime * 10;

            if (isPreviewItem)
            {
                cooldown = action.Recast100ms;
            }

            // only cache the last kMaxItemCount items
            if (_items.Count >= MaxItemCount)
            {
                _items.RemoveAt(0);
            }

            double now = ImGui.GetTime();
            float gcdDuration = 0;
            float castTime = 0;

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
            if (TimelineActionCatalog.CanonicalIds.TryGetValue(actionId, out uint replacedId))
            {
                type = TimelineItemType.Action;
                actionId = replacedId;
            }

            // Ninja's Hide restores Mudra Charges
            if (actionId == 2245)
            {
                _items.RemoveAll(item => TimelineActionCatalog.MudraActions.Contains(item.ActionID));
            }

            if (TimelineActionCatalog.MudraActions.Contains(actionId) && _hadMudra)
            {
                return;
            }

            TimelineKey key = TimelineKey.ForAction(actionId);
            TimelineItem? existingItem = _items.FirstOrDefault(x => x.Key == key);
            if (existingItem != null)
            {
                existingItem.TriggeredAt = now;
                existingItem.MaxCharges = maxCharges;
                existingItem.ActiveCharges = stacks;
                existingItem.Cooldown = cooldown;

                if (maxCharges > 1 && chargeTime > 0)
                {
                    float elapsedIntoNextCharge = Math.Clamp(
                        recastInfo.RecastTimeElapsed - stacks * chargeTime,
                        0,
                        chargeTime);
                    existingItem.Time = now - elapsedIntoNextCharge;
                }

                return;
            }

            // handle changed icons
            int iconId = TimelineActionCatalog.IconOverrides.TryGetValue(actionId, out uint hardcodedIconId) ? (int)hardcodedIconId : action.Icon;

            // calculate gcd and cast time
            if (type == TimelineItemType.CastStart)
            {
                gcdDuration = actionHelper.GetGCDTime(actionId);
                castTime = actionHelper.GetCastTime(actionId);
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
                    gcdDuration = actionHelper.GetGCDTime(actionId);
                    castTime = _hadSwiftcast ? 0 : actionHelper.GetCastTime(actionId);
                }
            }

            // handle more weird cases
            if (TimelineActionCatalog.RecastOverrides.TryGetValue(actionId, out float gcd))
            {
                type = TimelineItemType.Action;
                gcdDuration = gcd;
            }

            TimelineItem item = new TimelineItem(key, (uint)iconId, type, now, gcdDuration, castTime, cooldown, maxCharges)
            {
                ActiveCharges = stacks,
            };

            if (maxCharges > 1 && chargeTime > 0)
            {
                float elapsedIntoNextCharge = Math.Clamp(
                    recastInfo.RecastTimeElapsed - stacks * chargeTime,
                    0,
                    chargeTime);
                item.Time = now - elapsedIntoNextCharge;
            }

            _items.Add(item);
            SortTimeline();
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

            IPlayerCharacter ? player = IObjectTable.Get().LocalPlayer;
            if (player == null || actorId != player.GameObjectId) { return; }

            uint actionId = header->ActionId;
            ActionType actionType = (ActionType)header->ActionType;

            if (actionType == ActionType.Item)
            {
                if (Config.SourcesConfig.TrackItems)
                {
                    QueueUsedItem(actionId);
                }

                return;
            }

            if (actionType != ActionType.Action)
            {
                return;
            }

            if (TryFindPhantomActionSlot(actionId, out _))
            {
                if (Config.SourcesConfig.TrackPhantomActions)
                {
                    QueuePhantomAction(actionId);
                }

                return;
            }

            if (TryFindDutyActionSlot(actionId, out _))
            {
                TimelineSourceKind sourceKind = SourceKindForDutyAction(actionId);
                if (IsSourceEnabled(sourceKind))
                {
                    QueueDutyAction(actionId, sourceKind);
                }

                return;
            }

            TimelineItemType? type = TypeForActionID(actionId);
            if (!type.HasValue) { return; }

            AddItem (actionId, type.Value);
        }

        private void OnActorControl(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId, uint arg4, uint arg5, uint arg6, uint arg7, GameObjectId targetId, bool isRecorded)
        {
            _onActorControlHook?.Original(entityId, type, buffID, direct, actionId, sourceId, arg4, arg5, arg6, arg7, targetId, isRecorded);

            // Works for most wipes (it's the fadeout).
            if (direct == FadeoutDirectValue)
            {
                ResetCooldowns();
            }

            if (type != 15) { return; }

            IPlayerCharacter? player = IObjectTable.Get().LocalPlayer;
            if (player == null || entityId != player.GameObjectId) { return; }

            AddItem(actionId, TimelineItemType.CastCancel);
        }

        private unsafe void OnCast(uint sourceId, ActorCastPacket* packet)
        {
            _onActorCastHook?.Original(sourceId, packet);

            IPlayerCharacter? player = IObjectTable.Get().LocalPlayer;
            if (player == null || sourceId != player.GameObjectId) { return; }

            uint actionId = packet->SpellId;

            AddItem(actionId, TimelineItemType.CastStart);
        }
    }
}
