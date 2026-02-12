using System.ComponentModel;

namespace Recode.Core;

public enum Codec
{
    [Description("H.264")]
    H264,
    [Description("H.265")]
    H265,
    [Description("VP9")]
    Vp9,
    [Description("AV1")]
    Av1,
}