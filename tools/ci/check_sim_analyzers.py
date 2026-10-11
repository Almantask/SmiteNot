#!/usr/bin/env python3
"""Prove the determinism analyzers are wired into sim/src: probe files with banned constructs must fail the build.

GG0001 (sim/analyzers) rejects floating point; RS0030 (BannedApiAnalyzers + sim/BannedSymbols.txt) rejects
wall-clock time, System.Random, string hashing and ad-hoc threads.

Usage: python tools/ci/check_sim_analyzers.py
Exit 0 when every probe is rejected with its expected diagnostic, 1 otherwise. Standard library only.
"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROJECT = os.path.join(ROOT, "sim", "src", "GodGame.Sim", "GodGame.Sim.csproj")
PROBE = os.path.join(ROOT, "sim", "src", "GodGame.Sim", "BannedApiProbe.cs")

# name: (class members, expected diagnostic id)
PROBES = {
    "float x = 1f;": ("internal static int Probe() { float x = 1f; return (int)x; }", "GG0001"),
    "double field": ("private static int _n; internal static double Field => _n; ", "GG0001"),
    "var from Math.Sqrt": ("internal static int Probe() { var r = System.Math.Sqrt(4); return (int)r; }", "GG0001"),
    "System.Random": ("internal static int Probe() => new System.Random(1).Next();", "RS0030"),
    "DateTime.Now": ("internal static long Probe() => System.DateTime.Now.Ticks;", "RS0030"),
    "string.GetHashCode": ('internal static int Probe() => "a".GetHashCode();', "RS0030"),
    "Task.Run": ("internal static void Probe() => System.Threading.Tasks.Task.Run(() => { });", "RS0030"),
}

TEMPLATE = """namespace GodGame.Sim
{{
    internal static class BannedApiProbe
    {{
        {members}
    }}
}}
"""


def build():
    return subprocess.run(
        ["dotnet", "build", PROJECT, "-nologo", "-v", "q", "-p:RunAnalyzersDuringBuild=true"],
        cwd=ROOT, capture_output=True, text=True)


def main():
    failures = []
    try:
        for name, (members, expected) in PROBES.items():
            with open(PROBE, "w", encoding="utf-8", newline="\n") as f:
                f.write(TEMPLATE.format(members=members))
            result = build()
            output = result.stdout + result.stderr
            if result.returncode != 0 and expected in output:
                print(f"ok: {name} is rejected ({expected})")
            else:
                failures.append(name)
                print(f"FAIL: {name} was not rejected with {expected}\n{output[-2000:]}")
    finally:
        if os.path.exists(PROBE):
            os.remove(PROBE)

    clean = build()
    if clean.returncode != 0:
        print("FAIL: the project doesn't build cleanly without the probe\n" + (clean.stdout + clean.stderr)[-2000:])
        return 1
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
