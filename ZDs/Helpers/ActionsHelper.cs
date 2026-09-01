using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ZDs.Helpers;

public class ActionsHelper
{
    private readonly unsafe ActionManager* _actionManager = ActionManager.Instance();

    public unsafe RecastInfo GetAdjustedRecastInfo(uint actionId)
    {
        ushort maxCharges = ActionManager.GetMaxCharges(actionId, 0);
        float recastTime = _actionManager->GetRecastTime(ActionType.Action, actionId);
        float recastTimeElapsed = _actionManager->GetRecastTimeElapsed(ActionType.Action, actionId);
        float chargeTime = ActionManager.GetAdjustedRecastTime(ActionType.Action, actionId) / 1000f;

        if (chargeTime <= 0 && maxCharges > 0 && recastTime > 0)
        {
            chargeTime = recastTime / maxCharges;
        }

        return new RecastInfo(
            recastTime,
            recastTimeElapsed,
            chargeTime,
            maxCharges,
            _actionManager->GetCurrentCharges(actionId));
    }

    public unsafe NativeRecastInfo GetNativeRecastInfo(ActionType actionType, uint actionId)
    {
        return new NativeRecastInfo(
            _actionManager->GetRecastTime(actionType, actionId),
            _actionManager->GetRecastTimeElapsed(actionType, actionId));
    }

    public unsafe float GetGCDTime(uint actionId) => _actionManager->GetRecastTime(ActionType.Action, _actionManager->GetAdjustedActionId(actionId));
    public unsafe float GetCastTime(uint actionId) => ActionManager.GetAdjustedCastTime(ActionType.Action, _actionManager->GetAdjustedActionId(actionId)) / 1000f;
    public unsafe uint GetSpellActionId(uint actionId) => _actionManager->GetAdjustedActionId(actionId);
    public unsafe float GetRecastTime(uint actionId) => _actionManager->GetRecastTime(ActionType.Action, GetSpellActionId(actionId));
    public unsafe float GetRecastTimeElapsed(uint actionId) => _actionManager->GetRecastTimeElapsed(ActionType.Action, GetSpellActionId(actionId));
    public float GetSpellCooldown(uint actionId) => Math.Abs(GetRecastTime(GetSpellActionId(actionId)) - GetRecastTimeElapsed(GetSpellActionId(actionId)));

    public int GetSpellCooldownInt(uint actionId)
    {
        int cooldown = (int)Math.Ceiling(GetSpellCooldown(actionId) % GetRecastTime(actionId));
        return Math.Max(0, cooldown);
    }

    public int GetStackCount(int maxStacks, uint actionId)
    {
        int cooldown = GetSpellCooldownInt(actionId);
        float recastTime = GetRecastTime(actionId);

        if (cooldown <= 0 || recastTime == 0)
        {
            return maxStacks;
        }

        return maxStacks - (int)Math.Ceiling(cooldown / (recastTime / maxStacks));
    }

    public readonly struct RecastInfo(
        float recastTime,
        float recastTimeElapsed,
        float chargeTime,
        ushort maxCharges,
        uint currentCharges)
    {
        public float RecastTime { get; } = recastTime;
        public float RecastTimeElapsed { get; } = recastTimeElapsed;
        public float ChargeTime { get; } = chargeTime;
        public ushort MaxCharges { get; } = maxCharges;
        public uint CurrentCharges { get; } = currentCharges;
    }

    public readonly record struct NativeRecastInfo(float RecastTime, float RecastTimeElapsed)
    {
        public float Remaining => Math.Max(0, RecastTime - RecastTimeElapsed);
    }
}
