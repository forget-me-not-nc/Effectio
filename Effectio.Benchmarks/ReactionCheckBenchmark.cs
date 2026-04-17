using BenchmarkDotNet.Attributes;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Reactions;
using Effectio.Statuses;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// Measures ReactionEngine.CheckReactions with many registered reactions and a live entity.
    /// Targets the pooled-buffer hot path — expect 0 B/op after the refactor.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class ReactionCheckBenchmark
    {
        [Params(5, 25, 100)]
        public int ReactionCount { get; set; }

        private EffectioManager _manager = null!;
        private Effectio.Entities.IEffectioEntity _entity = null!;

        [GlobalSetup]
        public void Setup()
        {
            _manager = new EffectioManager();

            // Register N reactions, with the last one matching.
            for (int i = 0; i < ReactionCount - 1; i++)
            {
                _manager.Statuses.RegisterStatus(new Status("A" + i));
                _manager.Statuses.RegisterStatus(new Status("B" + i));
                _manager.Reactions.RegisterReaction(ReactionBuilder.Create("r_" + i)
                    .RequireStatuses("A" + i, "B" + i)
                    .Persists()
                    .ApplyStatus("out_" + i)
                    .Build());
            }

            _manager.Statuses.RegisterStatus(new Status("Burning"));
            _manager.Statuses.RegisterStatus(new Status("Wet"));
            _manager.Statuses.RegisterStatus(new Status("Vaporized"));
            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .Persists()
                .ApplyStatus("Vaporized")
                .Build());

            _entity = _manager.CreateEntity("p");
            _entity.AddStatus("Burning");
            _entity.AddStatus("Wet");
        }

        [Benchmark]
        public void CheckReactions()
        {
            _manager.Reactions.CheckReactions(_entity);
        }
    }
}
