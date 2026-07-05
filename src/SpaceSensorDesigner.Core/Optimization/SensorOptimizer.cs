using System;
using System.Collections.Generic;
using System.Linq;
using SpaceSensorDesigner.Core.Coverage;
using SpaceSensorDesigner.Core.Geometry;
using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Optimization;

/// <summary>Tuning knobs for <see cref="SensorOptimizer"/>.</summary>
public sealed class OptimizerOptions
{
    /// <summary>Stop once this fraction (0..1) of floor cells is covered.</summary>
    public double CoverageTarget { get; set; } = 0.95;

    /// <summary>Hard cap on the number of suggested sensors.</summary>
    public int MaxSensors { get; set; } = 12;

    /// <summary>Spacing (metres) of the candidate-position lattice.</summary>
    public double CandidateSpacing { get; set; } = 0.75;

    // Suggested-sensor template (a wide MLX90640 by default).
    public SensorType SensorType { get; set; } = SensorType.Activity;
    public double Height { get; set; } = 2.7;
    public double HorizontalFovDegrees { get; set; } = 110;
    public double VerticalFovDegrees { get; set; } = 75;
}

/// <summary>
/// A deterministic greedy set-cover optimizer for downward thermal sensors. It repeatedly places a
/// sensor at the candidate position whose footprint newly covers the most uncovered floor cells,
/// until the coverage target or the sensor budget is reached. Entirely local — no AI, no cloud.
/// </summary>
public static class SensorOptimizer
{
    public static List<Sensor> Optimize(FloorPlan plan, OptimizerOptions? options = null)
    {
        options ??= new OptimizerOptions();
        var obstacles = Obstacles.Build(plan);

        // 1. Which floor cells are still uncovered by the committed sensors?
        var grid = CoverageCalculator.Compute(plan).Grid;
        var uncovered = new List<Vec2>();
        int floorCount = 0;
        for (int c = 0; c < grid.Columns; c++)
        for (int r = 0; r < grid.Rows; r++)
        {
            if (!grid.IsFloor(c, r)) continue;
            floorCount++;
            if (grid[c, r] != CoverageState.Covered)
                uncovered.Add(grid.CellCenter(c, r));
        }

        var suggestions = new List<Sensor>();
        if (floorCount == 0 || uncovered.Count == 0)
            return suggestions;

        // 2. Candidate positions: a lattice sampled inside the floor area.
        var candidates = BuildCandidates(plan, options.CandidateSpacing);
        if (candidates.Count == 0)
            return suggestions;

        // Pre-compute, once, the set of uncovered cells each candidate position would cover.
        var candidateSets = new List<HashSet<int>>(candidates.Count);
        foreach (var pos in candidates)
        {
            var poly = SensorFootprint.FloorCorners(pos, options.Height, options.HorizontalFovDegrees, options.VerticalFovDegrees, 0);
            var set = new HashSet<int>();
            for (int idx = 0; idx < uncovered.Count; idx++)
            {
                var cell = uncovered[idx];
                if (!GeometryUtils.PointInPolygon(cell, poly)) continue;
                if (!GeometryUtils.HasLineOfSight(pos, cell, obstacles)) continue;
                set.Add(idx);
            }
            candidateSets.Add(set);
        }

        // 3. Greedy set cover: repeatedly take the candidate covering the most still-uncovered cells.
        var remaining = new HashSet<int>(Enumerable.Range(0, uncovered.Count));
        double targetRemaining = floorCount * (1.0 - options.CoverageTarget);
        var chosen = new List<int>(); // candidate indices

        while (chosen.Count < options.MaxSensors && remaining.Count > targetRemaining && remaining.Count > 0)
        {
            int bestGain = 0, bestCand = -1;
            for (int c = 0; c < candidateSets.Count; c++)
            {
                int gain = 0;
                foreach (var idx in candidateSets[c])
                    if (remaining.Contains(idx)) gain++;
                if (gain > bestGain) { bestGain = gain; bestCand = c; } // strict > → deterministic
            }
            if (bestCand < 0 || bestGain == 0) break;
            chosen.Add(bestCand);
            foreach (var idx in candidateSets[bestCand]) remaining.Remove(idx);
        }

        // 4. Local-search refinement: try relocating each chosen sensor to the candidate that
        //    maximises the total covered set given the others — escapes greedy's local optimum.
        for (int pass = 0; pass < 2; pass++)
        {
            bool improved = false;
            for (int i = 0; i < chosen.Count; i++)
            {
                var others = new HashSet<int>();
                for (int j = 0; j < chosen.Count; j++)
                    if (j != i) others.UnionWith(candidateSets[chosen[j]]);

                int baseCovered = CountUnion(others, candidateSets[chosen[i]]);
                int bestCand = chosen[i], bestCovered = baseCovered;
                for (int c = 0; c < candidateSets.Count; c++)
                {
                    int cov = CountUnion(others, candidateSets[c]);
                    if (cov > bestCovered) { bestCovered = cov; bestCand = c; }
                }
                if (bestCand != chosen[i]) { chosen[i] = bestCand; improved = true; }
            }
            if (!improved) break;
        }

        foreach (var c in chosen)
        {
            suggestions.Add(new Sensor
            {
                Type = options.SensorType,
                Name = $"Suggested {suggestions.Count + 1}",
                Position = candidates[c],
                Height = options.Height,
                HorizontalFovDegrees = options.HorizontalFovDegrees,
                VerticalFovDegrees = options.VerticalFovDegrees,
                IsSuggestion = true
            });
        }

        return suggestions;
    }

    private static int CountUnion(HashSet<int> a, HashSet<int> b)
    {
        int count = a.Count;
        foreach (var x in b)
            if (!a.Contains(x)) count++;
        return count;
    }

    private static List<Vec2> BuildCandidates(FloorPlan plan, double spacing)
    {
        bool hasRooms = plan.Rooms.Count > 0;

        // Sample a lattice over the candidate region: the rooms' extent if any, else the drawn walls'
        // bounding box (keeps suggestions inside the traced apartment), else the whole plan.
        var (min, max) = plan.GetBounds();
        if (!hasRooms && plan.WallBounds() is { } wb) (min, max) = wb;

        var result = new List<Vec2>();
        for (double x = min.X; x <= max.X; x += spacing)
        {
            for (double y = min.Y; y <= max.Y; y += spacing)
            {
                var p = new Vec2(x, y);
                if (hasRooms && !plan.Rooms.Any(rm => GeometryUtils.PointInPolygon(p, rm.Polygon)))
                    continue;
                result.Add(p);
            }
        }

        // Always include room centroids as strong candidates.
        foreach (var room in plan.Rooms)
            result.Add(room.Centroid);

        return result;
    }
}
