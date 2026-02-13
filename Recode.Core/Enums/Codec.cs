using System.ComponentModel;

namespace Recode.Core.Enums;

[AttributeUsage(AttributeTargets.Field)]
public class TooltipAttribute(string text) : Attribute
{
    public string Text { get; } = text;
}

public enum Codec
{
    [Description("H.264"), Tooltip("Best compatibility, fast encoding, widely supported by all devices")]
    H264,
    [Description("H.265"), Tooltip("Better compression than H.264 at same quality, slower encoding")]
    H265,
    [Description("VP9"), Tooltip("Google's open codec, good for web video, comparable to H.265")]
    Vp9,
    [Description("AV1"), Tooltip("Best compression, very slow encoding, growing device support")]
    Av1,
}