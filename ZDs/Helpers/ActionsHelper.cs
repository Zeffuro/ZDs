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
