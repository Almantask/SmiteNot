using BenchmarkDotNet.Running;

namespace GodGame.Sim.Bench;

public static class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
