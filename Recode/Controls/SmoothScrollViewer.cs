using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Recode.Controls;

public class SmoothScrollViewer : ScrollViewer
{
    public static readonly StyledProperty<double> ScrollStepProperty =
        AvaloniaProperty.Register<SmoothScrollViewer, double>(nameof(ScrollStep), 100);

    public double ScrollStep
    {
        get => GetValue(ScrollStepProperty);
        set => SetValue(ScrollStepProperty, value);
    }

    const double AnimationDuration = 170;

    double _targetOffset;
    double _startOffset;
    DateTime _animationStartTime;
    bool _isAnimating;

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        RemoveHandler(PointerWheelChangedEvent, OnPointerWheelChanged);
    }

    void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        double maxOffset = Math.Max(0, Extent.Height - Viewport.Height);

        if (maxOffset <= 0)
            return;

        double delta = -e.Delta.Y * ScrollStep;
        SineEaseOut easing = new();
        DateTime now = DateTime.Now;

        if (_isAnimating)
        {
            double elapsed = (now - _animationStartTime).TotalMilliseconds;
            double progress = Math.Min(elapsed / AnimationDuration, 1.0);
            _startOffset += easing.Ease(progress) * (_targetOffset - _startOffset);
            _targetOffset = Math.Clamp(_targetOffset + delta, 0, maxOffset);
            _animationStartTime = now;
        }
        else
        {
            _startOffset = Offset.Y;
            _targetOffset = Math.Clamp(_startOffset + delta, 0, maxOffset);
            _animationStartTime = now;
            _isAnimating = true;
            _ = Animate();
        }

        e.Handled = true;
    }

    async Task Animate()
    {
        var easing = new SineEaseOut();

        while (_isAnimating)
        {
            double elapsed = (DateTime.Now - _animationStartTime).TotalMilliseconds;

            if (elapsed >= AnimationDuration)
            {
                Offset = Offset.WithY(_targetOffset);
                _isAnimating = false;
                break;
            }

            double progress = elapsed / AnimationDuration;
            double current = _startOffset + easing.Ease(progress) * (_targetOffset - _startOffset);
            Offset = Offset.WithY(current);

            await Task.Delay(8);
        }
    }
}