// Fixtures run side by side: every test builds its own rules, map and simulation, and the simulation keeps no static
// state, so nothing is shared between them. Four at a time leaves cores for the tests that use a ThreadedRunner.
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(4)]
