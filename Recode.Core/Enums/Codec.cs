using System.ComponentModel;

namespace Recode.Core.Enums;

[AttributeUsage(AttributeTargets.Field)]
public class TooltipAttribute(string text) : Attribute
{
    public string Text { get; } = text;
}

public enum Codec
{
    [Description("H.264"), Tooltip("Fast encoding and balanced quality")]
    H264,
    [Description("H.265"), Tooltip("Better compression than H.264 at same quality, slower encoding")]
    H265,
    [Description("VP9"), Tooltip("Best compression for low quality settings, slower encoding, outputs .webm")]
    Vp9,
}