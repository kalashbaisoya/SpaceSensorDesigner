using System;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Telemetry;

/// <summary>
/// A deterministic, time-varying stand-in for a real device feed. Each sensor gets a stable
/// personality from its id (phase, base battery, rare-offline flag); values then breathe with the
/// clock so the panel animates. Occupancy drives a warm "peak" temperature, mimicking a body in
/// frame. Because <see cref="Sample"/> is a pure function of (sensor, time) it is fully unit-testable.
/// </summary>
public sealed class SimulatedTelemetrySource : ITelemetrySource
{
    public SensorTelemetry Sample(Sensor sensor, DateTime nowUtc)
    {
        int seed = Math.Abs(sensor.Id.GetHashCode());
        double phase = (seed % 628) / 100.0;              // 0..2π
        double t = nowUtc.TimeOfDay.TotalSeconds;

        // A small, stable fraction of sensors read as offline (never suggestions).
        bool online = !sensor.IsSuggestion && (seed % 17 != 0);
        int battery = Battery(seed, nowUtc);

        if (!online)
        {
            int agoMin = seed % 59 + 1;
            return new SensorTelemetry(sensor.Id, false, battery, 0, 0, 0, nowUtc.AddMinutes(-agoMin));
        }

        int occupancy = Math.Max(0, (int)Math.Round(1.6 + 1.8 * Math.Sin(t * 0.35 + phase)));
        double ambient = 20.5 + 1.8 * Math.Sin(t * 0.08 + phase);
        double peak = occupancy > 0
            ? ambient + 9.5 + occupancy * 0.9 + 0.4 * Math.Sin(t * 0.7 + phase)  // a warm body in frame
            : ambient + 0.6;

        return new SensorTelemetry(sensor.Id, true, battery, occupancy, Round1(ambient), Round1(peak), nowUtc);
    }

    private static int Battery(int seed, DateTime now)
    {
        int baseP = 55 + seed % 45; // 55..99, stable per sensor
        int drift = (int)Math.Round(3 * Math.Sin(now.TimeOfDay.TotalMinutes * 0.02 + (seed % 100) / 15.0));
        return Math.Clamp(baseP + drift, 1, 100);
    }

    private static double Round1(double v) => Math.Round(v, 1);
}
