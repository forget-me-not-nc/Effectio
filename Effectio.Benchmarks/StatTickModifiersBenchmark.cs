using BenchmarkDotNet.Attributes;
using Effectio.Modifiers;
using Effectio.Stats;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// Focused benchmark on Stat.TickModifiers — proves the modifier-expiration pass is
    /// allocation-free even with many duration-tracked modifiers.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class StatTickModifiersBenchmark
    {
        [Params(10, 100)]
        public int ModifierCount { get; set; }

        private Stat _stat = null!;

        [GlobalSetup]
        public void Setup()
        {
            _stat = new Stat("Damage", 100f);
            // Use a huge duration so modifiers never expire during the measurement.
            for (int i = 0; i < ModifierCount; i++)
                _stat.AddModifier(new AdditiveModifier("m_" + i, 1f, duration: float.MaxValue));
        }

        [Benchmark]
        public void TickModifiers()
        {
            // Tiny dt — measures pure iteration cost, zero expirations.
            _stat.TickModifiers(0.001f);
        }
    }
}
