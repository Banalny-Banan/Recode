using System;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;

namespace Recode.Controls;

public class SmoothProgressBar : ProgressBar
{
    public SmoothProgressBar() => Transitions =
    [
        new DoubleTransition
        {
            Property = ValueProperty,
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = new SineEaseOut(),
        },
    ];

    protected override Type StyleKeyOverride => typeof(ProgressBar);
}