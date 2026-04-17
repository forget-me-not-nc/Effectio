using BenchmarkDotNet.Attributes;
using Effectio.Modifiers;
using Effectio.Stats;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// Measures Stat.Recalculate() across varying modifier counts.
    /// Confirms the polymorphic single-pass pipeline scales linearly and allocation-free.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class StatRecalculateBenchmark
    {
        [Params(1, 10, 50, 200)]
        public int ModifierCount { get; set; }

        private Stat _stat = null!;

        [GlobalSetup]
        public void Setup()
        {
            _stat = new Stat("Damage", 100f, 0f, 100000f);
            // Mix of kinds to exercise all priority bands.
            for (int i = 0; i < ModifierCount; i++)
            {
                switch (i % 3)
                {
                    case 0: _stat.AddModifier(new AdditiveModifier($"add_{i}", 1f)); break;
                    case 1: _stat.AddModifier(new MultiplicativeModifier($"mul_{i}", 1.001f)); break;
                    case 2: _stat.AddModifier(new CapAdjustmentModifier($"cap_{i}", 10f)); break;
                }
            }
        }

        [Benchmark]
        public float Recalculate()
        {
            _stat.Recalculate();
            return _stat.CurrentValue;
        }
    }
}
