using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using GodGame.Bridge;
using GodGame.Sim;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GodGame.Benchmarks
{
    /// <summary>
    /// Runs the Burst decision's benchmarks in whatever runtime hosts it (the Mono editor, or the IL2CPP player with
    /// <c>-run-bench &lt;file&gt; -repo &lt;repo&gt;</c>): the separation kernel managed and under Burst, with checksums,
    /// and a whole simulation tick (10,000 units on m1-flat, one thread). Results go to a text file.
    /// </summary>
    public static class KernelBench
    {
        public const string RunArgument = "-run-bench";

        public static string Run(string repoRoot)
        {
            var report = new StringBuilder();
            report
                .Append("runtime ")
                .Append(Application.isEditor ? "editor-mono" : "player")
                .Append(' ');
            report.Append(Runtime()).Append('\n');

            int columns = (int)(100L * 65536 / SeparationKernel.CellRaw) + 1;
            int rows = (int)(77L * 65536 / SeparationKernel.CellRaw) + 1;
            var x = new long[SeparationKernel.Units];
            var y = new long[SeparationKernel.Units];
            SeparationKernel.MakeCrowd(x, y);
            var cellStart = new int[columns * rows + 1];
            var cellUnits = new int[x.Length];
            SeparationKernel.BuildGrid(x, y, columns, rows, cellStart, cellUnits);
            var px = new long[x.Length];
            var py = new long[x.Length];
            var pr = new long[x.Length];

            const int Repeats = 30;
            double managed = Time(
                Repeats,
                () =>
                    SeparationKernel.Run(
                        x,
                        y,
                        columns,
                        rows,
                        cellStart,
                        cellUnits,
                        px,
                        py,
                        pr,
                        0,
                        x.Length
                    )
            );
            ulong managedSum = SeparationKernel.Checksum(px, py, pr);

            var nx = new NativeArray<long>(x, Allocator.Persistent);
            var ny = new NativeArray<long>(y, Allocator.Persistent);
            var ns = new NativeArray<int>(cellStart, Allocator.Persistent);
            var nu = new NativeArray<int>(cellUnits, Allocator.Persistent);
            var npx = new NativeArray<long>(x.Length, Allocator.Persistent);
            var npy = new NativeArray<long>(x.Length, Allocator.Persistent);
            var npr = new NativeArray<long>(x.Length, Allocator.Persistent);
            try
            {
                var job = new BurstSeparation
                {
                    X = nx,
                    Y = ny,
                    CellStart = ns,
                    CellUnits = nu,
                    PushX = npx,
                    PushY = npy,
                    Pressure = npr,
                    Columns = columns,
                    Rows = rows,
                };
                double burst = Time(Repeats, () => job.Run());
                ulong burstSum = SeparationKernel.Checksum(
                    npx.ToArray(),
                    npy.ToArray(),
                    npr.ToArray()
                );
                report.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "separation_managed_ms {0:0.000}\n",
                    managed
                );
                report.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "separation_burst_ms {0:0.000}\n",
                    burst
                );
                report.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "separation_checksum_managed {0:X16}\n",
                    managedSum
                );
                report.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "separation_checksum_burst {0:X16}\n",
                    burstSum
                );
                report.Append(
                    managedSum == burstSum
                        ? "separation_checksums_match yes\n"
                        : "separation_checksums_match NO\n"
                );
            }
            finally
            {
                nx.Dispose();
                ny.Dispose();
                ns.Dispose();
                nu.Dispose();
                npx.Dispose();
                npy.Dispose();
                npr.Dispose();
            }

            // A whole tick of the real simulation, on one thread.
            var rules = RepoLoader.LoadRules(repoRoot);
            var map = RepoLoader.LoadMap(repoRoot, "m1-flat");
            var plan = RepoLoader.LoadPlan(repoRoot, "m1-ten-thousand.json", map, rules);
            var simulation = new Battle(rules, map, plan, 1).Start(new SerialRunner());
            simulation.Step(100); // warm up and get marching
            double tick = Time(200, () => simulation.Step());
            report.AppendFormat(
                CultureInfo.InvariantCulture,
                "tick_ms_10k_m1_flat_serial {0:0.000}\n",
                tick
            );
            return report.ToString();
        }

        private static double Time(int repeats, Action action)
        {
            action(); // JIT, Burst compile, caches
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < repeats; i++)
            {
                action();
            }
            return clock.Elapsed.TotalMilliseconds / repeats;
        }

        private static string Runtime()
        {
#if ENABLE_IL2CPP
            return "scripting IL2CPP";
#else
            return "scripting Mono";
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, RunArgument);
            if (at < 0 || at + 1 >= args.Length)
            {
                return;
            }
            int repoAt = Array.IndexOf(args, "-repo");
            string repo =
                repoAt >= 0 && repoAt + 1 < args.Length
                    ? args[repoAt + 1]
                    : RepoLoader.EditorRepoRoot;
            try
            {
                File.WriteAllText(args[at + 1], Run(repo));
                Application.Quit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText(args[at + 1], "FAILED " + e);
                Application.Quit(1);
            }
        }
    }
}
