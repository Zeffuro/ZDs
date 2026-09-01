using System;
using System.Collections.Generic;
using System.Linq;
using ZDs.Config;

namespace ZDs.Timeline;

public interface ITimelineScale
{
    float Map(float remaining, float duration, float horizon, bool smoothArrival);
}

public sealed class PowerTimelineScale(float compression) : ITimelineScale
{
    public float Map(float remaining, float duration, float horizon, bool smoothArrival)
    {
        double safeHorizon = Math.Max(1, horizon);
        double time = Math.Clamp(remaining, 0, safeHorizon);
        double safeCompression = Math.Max(0, compression);
        double denominator = Math.Pow(safeHorizon, safeCompression);

        if (smoothArrival && safeCompression is > 0 and < 1 && time < 2)
        {
            double tailTime = Math.Min(2, safeHorizon);
            double tailPosition = Math.Pow(tailTime, safeCompression) / denominator;
            double progress = time / tailTime;
            double easedProgress = (safeCompression - 2) * Math.Pow(progress, 3) +
                                   (3 - safeCompression) * Math.Pow(progress, 2);
            return (float)(tailPosition * easedProgress);
        }

        return (float)(Math.Pow(time, safeCompression) / denominator);
    }
}

public sealed class PiecewiseTimelineScale(IEnumerable<TimelineScalePoint> configuredPoints) : ITimelineScale
{
    private readonly (float Seconds, float Position)[] _points = configuredPoints
        .Where(point => point != null)
        .Select(point => (point.Seconds, point.Position))
        .OrderBy(point => point.Seconds)
        .Take(3)
        .ToArray();

    public float Map(float remaining, float duration, float horizon, bool smoothArrival)
    {
        float safeHorizon = Math.Max(1, horizon);
        float time = Math.Clamp(remaining, 0, safeHorizon);
        float previousSeconds = 0f;
        float previousPosition = 0f;

        foreach ((float secondsValue, float positionValue) in _points)
        {
            float seconds = Math.Clamp(secondsValue, previousSeconds + 0.001f, safeHorizon);
            float position = Math.Clamp(positionValue, previousPosition + 0.001f, 1f);
            if (time <= seconds)
            {
                float span = seconds - previousSeconds;
                float progress = span <= 0 ? 0 : (time - previousSeconds) / span;
                return Math.Clamp(previousPosition + (position - previousPosition) * progress, 0f, 1f);
            }

            previousSeconds = seconds;
            previousPosition = position;
        }

        float finalSpan = safeHorizon - previousSeconds;
        float finalProgress = finalSpan <= 0 ? 1f : (time - previousSeconds) / finalSpan;
        return Math.Clamp(previousPosition + (1f - previousPosition) * finalProgress, 0f, 1f);
    }
}

public sealed class PercentageTimelineScale : ITimelineScale
{
    public float Map(float remaining, float duration, float horizon, bool smoothArrival)
    {
        float safeDuration = Math.Max(0.001f, duration);
        return Math.Clamp(remaining / safeDuration, 0f, 1f);
    }
}

public static class TimelineScaleFactory
{
    public static ITimelineScale Create(ZDsConfig config) =>
        config.LayoutConfig.ScaleMode switch
        {
            TimelineScaleMode.Piecewise => new PiecewiseTimelineScale(config.LayoutConfig.ScalePoints),
            TimelineScaleMode.Percentage => new PercentageTimelineScale(),
            _ => new PowerTimelineScale(config.GeneralConfig.TimelineCompression),
        };
}
