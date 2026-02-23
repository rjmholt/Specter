using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Specter.Benchmarks.AnalysisBenchmarks).Assembly).Run(args);
