using System;
using System.Windows.Threading;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Telemetry;

namespace SpaceSensorDesigner.App.Services;

/// <summary>
/// Drives live sensor readings on a UI timer. Ships the <see cref="SimulatedTelemetrySource"/>;
/// swap in a real <see cref="ITelemetrySource"/> to read actual hardware. Raises <see cref="Updated"/>
/// on each tick so the properties panel can refresh.
/// </summary>
public sealed class LiveTelemetryService
{
    private readonly ITelemetrySource _source;
    private readonly DispatcherTimer _timer;

    /// <summary>True while readings come from the built-in simulator rather than real devices.</summary>
    public bool IsSimulated { get; }

    public event Action? Updated;

    public LiveTelemetryService(ITelemetrySource? source = null)
    {
        _source = source ?? new SimulatedTelemetrySource();
        IsSimulated = source is null or SimulatedTelemetrySource;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _timer.Tick += (_, _) => Updated?.Invoke();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    public SensorTelemetry Sample(Sensor sensor) => _source.Sample(sensor, DateTime.UtcNow);
}
