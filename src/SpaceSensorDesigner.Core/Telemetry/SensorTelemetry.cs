using System;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Telemetry;

/// <summary>An instantaneous reading from a thermal sensor.</summary>
public readonly record struct SensorTelemetry(
    Guid SensorId,
    bool Online,
    int BatteryPercent,
    int Occupancy,
    double AmbientTempC,
    double PeakTempC,
    DateTime LastSeenUtc);

/// <summary>
/// Supplies live readings for a sensor. The app ships a <see cref="SimulatedTelemetrySource"/>;
/// a real deployment would implement this over the device's API / MQTT / BLE feed.
/// </summary>
public interface ITelemetrySource
{
    SensorTelemetry Sample(Sensor sensor, DateTime nowUtc);
}
