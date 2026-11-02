using BenchmarkDotNet.Attributes;
using GodGame.Benchmarks;

namespace GodGame.Sim.Bench;

/// <summary>
/// The Burst decision's kernel on .NET (plan, M2 item 10; ADR 0002): neighbour separation over a dense 10,000-soldier
/// crowd, the same source file Unity times under Mono, IL2CPP and Burst (KernelBench). Prints the checksum once, so
/// the runs can be compared for exactness too.
/// </summary>
[MemoryDiagnoser]
public class SeparationKernelBench
{
    private long[] _x = null!;
    private long[] _y = null!;
    private int[] _cellStart = null!;
    private int[] _cellUnits = null!;
    private long[] _px = null!;
    private long[] _py = null!;
    private long[] _pr = null!;
    private int _columns;
    private int _rows;

    [GlobalSetup]
    public void Setup()
    {
        _columns = (int)(100L * 65536 / SeparationKernel.CellRaw) + 1;
        _rows = (int)(77L * 65536 / SeparationKernel.CellRaw) + 1;
        _x = new long[SeparationKernel.Units];
        _y = new long[SeparationKernel.Units];
        SeparationKernel.MakeCrowd(_x, _y);
        _cellStart = new int[_columns * _rows + 1];
        _cellUnits = new int[_x.Length];
        SeparationKernel.BuildGrid(_x, _y, _columns, _rows, _cellStart, _cellUnits);
        _px = new long[_x.Length];
        _py = new long[_x.Length];
        _pr = new long[_x.Length];
        Separation();
        Console.WriteLine(
            "separation_checksum " + SeparationKernel.Checksum(_px, _py, _pr).ToString("X16")
        );
    }

    [Benchmark]
    public void Separation() =>
        SeparationKernel.Run(
            _x,
            _y,
            _columns,
            _rows,
            _cellStart,
            _cellUnits,
            _px,
            _py,
            _pr,
            0,
            _x.Length
        );
}
