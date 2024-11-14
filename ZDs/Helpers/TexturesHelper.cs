﻿using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Component.Excel;
using Lumina.Excel;

namespace ZDs.Helpers
{
    public class TexturesHelper
    {
        public static IDalamudTextureWrap? GetTexture<T>(uint rowId, uint stackCount = 0, bool hdIcon = true) where T : struct, IExcelRow<T>
        {
            var sheet = Plugin.DataManager.GetExcelSheet<T>();
            return sheet == null ? null : GetTexture<T>(sheet.GetRow(rowId), stackCount, hdIcon);
        }

        public static IDalamudTextureWrap? GetTexture<T>(dynamic row, uint stackCount = 0, bool hdIcon = true) where T : struct, IExcelRow<T>
        {
            var iconId = row.Icon;
            return GetTextureFromIconId(iconId, stackCount, hdIcon);
        }

        public static IDalamudTextureWrap? GetTextureFromIconId(uint iconId, uint stackCount = 0, bool hdIcon = true)
        {
            GameIconLookup lookup = new GameIconLookup(iconId + stackCount, false, hdIcon);
            return Plugin.TextureProvider.GetFromGameIcon(lookup).GetWrapOrDefault();
        }

        public static IDalamudTextureWrap? GetTextureFromPath(string path)
        {
            return Plugin.TextureProvider.GetFromGame(path).GetWrapOrDefault();
        }
    }
}
