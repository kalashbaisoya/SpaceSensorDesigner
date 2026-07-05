using System;
using SpaceSensorDesigner.Core.Export;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Telemetry;
using Xunit;

namespace SpaceSensorDesigner.Tests;

public class Phase5Tests
{
    private static readonly DateTime T0 = new(2026, 7, 4, 13, 20, 0, DateTimeKind.Utc);

    [Fact]
    public void Telemetry_IsDeterministicForSameSensorAndTime()
    {
        var src = new SimulatedTelemetrySource();
        var s = new Sensor { Position = new Vec2(2, 2), Height = 2.7 };

        var a = src.Sample(s, T0);
        var b = src.Sample(s, T0);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Telemetry_OnlineSensorHasSaneReadings()
    {
        var src = new SimulatedTelemetrySource();
        // Try several ids until we hit an "online" one (a small fraction read offline).
        for (int i = 0; i < 50; i++)
        {
            var s = new Sensor();
            var t = src.Sample(s, T0);
            Assert.InRange(t.BatteryPercent, 1, 100);
            if (t.Online)
            {
                Assert.True(t.Occupancy >= 0);
                Assert.True(t.PeakTempC >= t.AmbientTempC); // a body in frame is never colder than ambient
                return;
            }
        }
        Assert.Fail("Expected at least one online sensor across 50 samples.");
    }

    [Fact]
    public void Telemetry_SuggestionIsOffline()
    {
        var src = new SimulatedTelemetrySource();
        var s = new Sensor { IsSuggestion = true };

        Assert.False(src.Sample(s, T0).Online);
    }

    [Fact]
    public void Report_CountsSensorsAndBom()
    {
        var project = DesignProject.CreateSample(); // one floor, one MLX90640 sensor
        project.Floors[0].Sensors.Add(new Sensor
        {
            Name = "Extra", Position = new Vec2(4, 2), Height = 2.7,
            HorizontalFovDegrees = 110, VerticalFovDegrees = 75
        });

        var report = CoverageReport.Build(project);

        Assert.Equal(2, report.TotalSensors);
        Assert.Equal(2, report.Sensors.Count);
        // Both share the 110×75 model → a single BOM line of qty 2.
        Assert.Single(report.Bom);
        Assert.Equal(2, report.Bom[0].Count);
    }

    [Fact]
    public void Report_CsvHasHeaderAndRowPerSensor()
    {
        var project = DesignProject.CreateSample();
        var csv = CoverageReport.ToCsv(CoverageReport.Build(project));

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Floor,Sensor,Model", lines[0]);
        Assert.Equal(2, lines.Length); // header + 1 sample sensor
    }

    [Fact]
    public void Report_HtmlIsSelfContainedAndNamed()
    {
        var project = DesignProject.CreateSample();
        var html = CoverageReport.ToHtml(CoverageReport.Build(project));

        Assert.Contains("<!doctype html>", html);
        Assert.Contains(project.Name, html);
        Assert.Contains("Bill of materials", html);
        Assert.DoesNotContain("http://", html); // no external resources
    }
}
