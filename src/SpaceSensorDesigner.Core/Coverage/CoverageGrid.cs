using SpaceSensorDesigner.Core.Models;

namespace SpaceSensorDesigner.Core.Coverage;

/// <summary>
/// A rectangular grid of <see cref="CoverageState"/> cells covering the plan bounds.
/// Cell (0,0) is at world position <see cref="Origin"/>; each cell is <see cref="CellSize"/> metres.
/// </summary>
public sealed class CoverageGrid
{
    public Vec2 Origin { get; }
    public double CellSize { get; }
    public int Columns { get; }
    public int Rows { get; }

    private readonly CoverageState[,] _cells;
    private readonly bool[,] _floor;
    private readonly double[,] _confidence;
    private readonly int[,] _coverCount;

    public CoverageGrid(Vec2 origin, double cellSize, int columns, int rows)
    {
        Origin = origin;
        CellSize = cellSize;
        Columns = columns;
        Rows = rows;
        _cells = new CoverageState[columns, rows];
        _floor = new bool[columns, rows];
        _confidence = new double[columns, rows];
        _coverCount = new int[columns, rows];
    }

    /// <summary>Detection confidence (0..1) for a covered cell — falls off toward the footprint edge.</summary>
    public double Confidence(int col, int row) => _confidence[col, row];
    public void SetConfidence(int col, int row, double value) => _confidence[col, row] = value;

    /// <summary>How many sensors cover this cell (with clear line of sight). ≥2 means redundant overlap.</summary>
    public int CoverCount(int col, int row) => _coverCount[col, row];
    public void SetCoverCount(int col, int row, int value) => _coverCount[col, row] = value;

    public CoverageState this[int col, int row]
    {
        get => _cells[col, row];
        set => _cells[col, row] = value;
    }

    /// <summary>
    /// True when the cell is part of the floor area (inside a room, or everywhere when the
    /// plan has no rooms). Non-floor cells are excluded from coverage stats and not painted.
    /// </summary>
    public bool IsFloor(int col, int row) => _floor[col, row];

    public void SetFloor(int col, int row, bool value) => _floor[col, row] = value;

    /// <summary>World-space centre of a cell.</summary>
    public Vec2 CellCenter(int col, int row)
        => new(Origin.X + (col + 0.5) * CellSize, Origin.Y + (row + 0.5) * CellSize);

    /// <summary>Builds an empty grid sized to the plan's bounds.</summary>
    public static CoverageGrid ForPlan(FloorPlan plan)
    {
        var (min, max) = plan.GetBounds();
        double cell = plan.CellSize <= 0 ? 0.2 : plan.CellSize;
        int cols = (int)System.Math.Ceiling((max.X - min.X) / cell);
        int rows = (int)System.Math.Ceiling((max.Y - min.Y) / cell);
        cols = System.Math.Max(1, cols);
        rows = System.Math.Max(1, rows);
        return new CoverageGrid(min, cell, cols, rows);
    }
}

/// <summary>Aggregate coverage numbers for the status bar / properties panel.</summary>
public readonly struct CoverageSummary
{
    public int TotalCells { get; }
    public int CoveredCells { get; }
    public int PartialCells { get; }
    public int UncoveredCells { get; }

    /// <summary>Covered cells seen by two or more sensors (redundant overlap).</summary>
    public int RedundantCells { get; }

    public CoverageSummary(int total, int covered, int partial, int uncovered, int redundant = 0)
    {
        TotalCells = total;
        CoveredCells = covered;
        PartialCells = partial;
        UncoveredCells = uncovered;
        RedundantCells = redundant;
    }

    /// <summary>Fraction (0..1) of cells fully covered.</summary>
    public double CoveredFraction => TotalCells == 0 ? 0 : (double)CoveredCells / TotalCells;

    public double CoveredPercent => CoveredFraction * 100.0;
}

/// <summary>Coverage numbers for a single room.</summary>
public readonly struct RoomCoverage
{
    public string Name { get; }
    public int TotalCells { get; }
    public int CoveredCells { get; }

    public RoomCoverage(string name, int total, int covered)
    {
        Name = name;
        TotalCells = total;
        CoveredCells = covered;
    }

    public double CoveredFraction => TotalCells == 0 ? 0 : (double)CoveredCells / TotalCells;
    public double CoveredPercent => CoveredFraction * 100.0;
}

/// <summary>Full result of a coverage computation: the grid, the overall summary, and per-room stats.</summary>
public sealed class CoverageResult
{
    public required CoverageGrid Grid { get; init; }
    public required CoverageSummary Summary { get; init; }
    public required System.Collections.Generic.IReadOnlyList<RoomCoverage> Rooms { get; init; }
}
