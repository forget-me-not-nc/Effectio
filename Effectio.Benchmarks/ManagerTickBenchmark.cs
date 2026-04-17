using BenchmarkDotNet.Attributes;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Modifiers;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// End-to-end EffectioManager.Tick benchmark with a realistic mixed-effect workload:
    /// every entity has stats, a periodic DoT, a timed buff, an aura, and a triggered low-HP heal.
    /// Proves the tick hot path is allocation-free at steady state.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class ManagerTickBenchmark
    {
        [Params(10, 100, 1000)]
        public int EntityCount { get; set; }

        private EffectioManager _manager = null!;

        [GlobalSetup]
        public void Setup()
        {
            _manager = new EffectioManager();
            _manager.Statuses.RegisterStatus(StatusBuilder.Create("Burning").WithDuration(100f).WithTickInterval(1f).Build());

            var dot    = EffectBuilder.Create("dot").Periodic(100f, 0.5f).AdjustStat("Health", -1f).Build();
            var buff   = EffectBuilder.Create("buff").Timed(50f).ApplyModifier("Damage", 5f).Build();
            var aura   = EffectBuilder.Create("aura").Aura(100f).AdjustStat("Armor", 10f).Build();
            var hurtBuff = EffectBuilder.Create("pwr").Timed(30f)
                .ApplyModifier("Damage", e => new MultiplicativeModifier(e.Key + "_mod", 1.25f, e.Duration, e.Key))
                .Build();
            var lastStand = EffectBuilder.Create("last").Triggered(100f)
                .AdjustStat("Health", 20f).WhenStatBelow("Health", 10f).Build();

            for (int i = 0; i < EntityCount; i++)
            {
                var e = _manager.CreateEntity("e_" + i);
                e.AddStat(new Stat("Health", 100f, 0f, 100f));
                e.AddStat(new Stat("Damage", 10f, 0f, 1000f));
                e.AddStat(new Stat("Armor", 0f, 0f, 1000f));

                _manager.Effects.ApplyEffect(e, dot);
                _manager.Effects.ApplyEffect(e, buff);
                _manager.Effects.ApplyEffect(e, aura);
                _manager.Effects.ApplyEffect(e, hurtBuff);
                _manager.Effects.ApplyEffect(e, lastStand);
                _manager.Statuses.ApplyStatus(e, "Burning");
            }
        }

        [Benchmark]
        public void Tick()
        {
            // 60 Hz frame — dt small enough that most effects do not expire mid-benchmark.
            _manager.Tick(1f / 60f);
        }
    }
}
