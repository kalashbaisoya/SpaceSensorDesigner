using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Optimization;
using Xunit;

namespace SpaceSensorDesigner.Tests;

public class OptimizerTests
{
    private static FloorPlan EmptyRoom(double w = 6, double h = 6)
    {
        var plan = new FloorPlan { CellSize = 0.25 };
        plan.Rooms.Add(new Room
        {
            Name = "Room", Type = RoomType.Other,
            Polygon = { new Vec2(0, 0), new Vec2(w, 0), new Vec2(w, h), new Vec2(0, h) }
        });
        return plan;
    }

    [Fact]
    public void Optimize_EmptyRoom_SuggestsAtLeastOneSensor()
    {
        var suggestions = SensorOptimizer.Optimize(EmptyRoom());

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.True(s.IsSuggestion));
    }

    [Fact]
    public void Optimize_ImprovesCoverage_TowardsTarget()
    {
        var plan = EmptyRoom();
        var before = CoverageCalculator.Compute(plan).Summary;

        var suggestions = SensorOptimizer.Optimize(plan, new OptimizerOptions { CoverageTarget = 0.9 });
        foreach (var s in suggestions) { s.IsSuggestion = false; plan.Sensors.Add(s); }

        var after = CoverageCalculator.Compute(plan).Summary;

        Assert.True(after.CoveredFraction > before.CoveredFraction);
        Assert.True(after.CoveredFraction >= 0.85, $"Coverage after optimize was {after.CoveredPercent:0.#}%");
    }

    [Fact]
    public void Optimize_IsDeterministic()
    {
        var a = SensorOptimizer.Optimize(EmptyRoom());
        var b = SensorOptimizer.Optimize(EmptyRoom());

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Position.X, b[i].Position.X, 6);
            Assert.Equal(a[i].Position.Y, b[i].Position.Y, 6);
        }
    }

    [Fact]
    public void Optimize_RespectsMaxSensorBudget()
    {
        // Large room + narrow sensors → far more than 3 needed, so the budget caps it.
        var plan = EmptyRoom(20, 20);
        var suggestions = SensorOptimizer.Optimize(plan, new OptimizerOptions
        {
            MaxSensors = 3, HorizontalFovDegrees = 55, VerticalFovDegrees = 35, Height = 2.7
        });

        Assert.True(suggestions.Count <= 3);
        Assert.NotEmpty(suggestions);
    }
}
