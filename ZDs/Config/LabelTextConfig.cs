using ZDs.Helpers;

namespace ZDs.Config;

public enum LabelTextEffect
{
    Outline = 0,
    Shadow = 1,
    None = 2
}

public sealed class LabelTextConfig
{
    public string FontFamily = FontsManager.DefaultFontFamily;
    public int FontSize = FontsManager.DefaultMediumFontSize;
    public ConfigColor Color = new(1f, 1f, 1f, 1f);
    public LabelTextEffect Effect = LabelTextEffect.Outline;
    public ConfigColor EffectColor = new(0f, 0f, 0f, 1f);
    public int ShadowOffset = 1;

    public LabelTextConfig()
    {
    }

    public LabelTextConfig(int fontSize, ConfigColor color, ConfigColor effectColor)
    {
        FontSize = fontSize;
        Color = color;
        EffectColor = effectColor;
    }

    public void MigrateFont(string? legacyKey)
    {
        if (!FontsManager.TryParseFontReference(legacyKey, out string family, out int size))
        {
            return;
        }

        FontFamily = family;
        if (size > 0)
        {
            FontSize = size;
        }
    }
}
