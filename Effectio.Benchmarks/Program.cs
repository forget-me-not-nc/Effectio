using BenchmarkDotNet.Running;

namespace Effectio.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Run a specific benchmark via:  dotnet run -c Release --project Effectio.Benchmarks -- --filter *TickBenchmark*
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
