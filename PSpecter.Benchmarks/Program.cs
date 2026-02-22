using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(PSpecter.Benchmarks.AnalysisBenchmarks).Assembly).Run(args);
