using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ZDs.Helpers;

public class ActionsHelper
{
    private readonly unsafe ActionManager* _actionManager;
    
    public unsafe ActionsHelper()
    {
        _actionManager = ActionManager.Instance();
    }
    
    public unsafe void GetAdjustedRecastInfo(uint actionId, out RecastInfo recastInfo)
    {
        recastInfo = default;
        int recastGroup = _actionManager->GetRecastGroup((int)ActionType.Action, actionId);
        RecastDetail* recastDetail = _actionManager->GetRecastGroupDetail(recastGroup);
        if (recastDetail == null)
        {
            return;
        }

        recastInfo.RecastTime = recastDetail->Total;
        recastInfo.RecastTimeElapsed = recastDetail->Elapsed;
        recastInfo.MaxCharges = ActionManager.GetMaxCharges(actionId, 100);
        if (recastInfo.MaxCharges == 1)
        {
            return;
        }

        ushort currentMaxCharges = ActionManager.GetMaxCharges(actionId, 0);
        if (currentMaxCharges == recastInfo.MaxCharges)
        {
            return;
        }

        recastInfo.RecastTime = (recastInfo.RecastTime * currentMaxCharges) / recastInfo.MaxCharges;
        recastInfo.MaxCharges = currentMaxCharges;
        if (recastInfo.RecastTimeElapsed > recastInfo.RecastTime)
        {
            recastInfo.RecastTime = 0;
            recastInfo.RecastTimeElapsed = 0;
        }

        return;
    }
    
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
    
    public struct RecastInfo
    {
        public float RecastTime;
        public float RecastTimeElapsed;
        public ushort MaxCharges;

        public RecastInfo(float recastTime, float recastTimeElapsed, ushort maxCharges)
        {
            RecastTime = recastTime;
            RecastTimeElapsed = recastTimeElapsed;
            MaxCharges = maxCharges;
        }
    }
}
