using System.Collections.Generic;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Effects.Actions;
using Effectio.Entities;
using Effectio.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Effectio.Tests.Effects
{
    /// <summary>
    /// An action can find out which status it is running for, and therefore how many of that
    /// status the entity has.
    ///
    /// The alternative these tests exist to avoid is a scale-by-stacks action written once per
    /// status, each with its own key baked into its constructor - which is the same class
    /// copied as many times as the game has stacking statuses.
    /// </summary>
    [TestClass]
    public class SourceStatusContextTests
    {
        /// <summary>Records the source key it was told, once per execution.</summary>
        private sealed class RecordingAction : IEffectAction
        {
            public readonly List<string> Sources = new List<string>();

            public void Execute(in EffectActionContext ctx) => Sources.Add(ctx.SourceStatusKey ?? "<none>");
            public void Undo(in EffectActionContext ctx) { }
        }

        /// <summary>The action this whole thing is for: one class, any stacking status.</summary>
        private sealed class DamagePerStackAction : IEffectAction
        {
            private readonly string _statKey;
            private readonly float _perStack;

            public DamagePerStackAction(string statKey, float perStack)
            {
                _statKey = statKey;
                _perStack = perStack;
            }

            public void Execute(in EffectActionContext ctx)
            {
                var stacks = ctx.SourceStatusKey == null
                    ? 1
                    : ctx.StatusEngine.GetStacks(ctx.Entity, ctx.SourceStatusKey);

                var stat = ctx.Entity.GetStat(_statKey);
                stat.BaseValue -= _perStack * stacks;
                stat.Recalculate();
            }

            public void Undo(in EffectActionContext ctx) { }
        }

        private static EffectioManager Victim(out IEffectioEntity entity)
        {
            var manager = new EffectioManager();
            entity = manager.CreateEntity("victim");
            entity.AddStat(new Stat("Health", 100f, 0f, 100f));
            return manager;
        }

        [TestMethod]
        public void EffectAppliedDirectly_HasNoSourceStatus()
        {
            var manager = Victim(out var entity);
            var action = new RecordingAction();

            manager.Effects.ApplyEffect(entity, EffectBuilder.Create("direct").Instant().WithAction(action).Build());

            // Null is a real answer: nothing owns this, and an action that scales by stacks
            // should read that as one.
            CollectionAssert.AreEqual(new[] { "<none>" }, action.Sources);
        }

        [TestMethod]
        public void StatusEffects_NameTheirStatus_OnEveryHook()
        {
            var manager = Victim(out var entity);
            var onApply = new RecordingAction();
            var onTick = new RecordingAction();
            var onRefresh = new RecordingAction();
            var onRemove = new RecordingAction();

            manager.Statuses.RegisterStatus(StatusBuilder.Create("burning")
                .WithDuration(10f)
                .Stackable(3)
                .WithTickInterval(1f)
                .OnApply(EffectBuilder.Create("b.apply").Instant().WithAction(onApply).Build())
                .OnTick(EffectBuilder.Create("b.tick").Instant().WithAction(onTick).Build())
                .OnRefresh(EffectBuilder.Create("b.refresh").Instant().WithAction(onRefresh).Build())
                .OnRemove(EffectBuilder.Create("b.remove").Instant().WithAction(onRemove).Build())
                .Build());

            manager.Statuses.ApplyStatus(entity, "burning");
            manager.Tick(1f);
            manager.Statuses.ApplyStatus(entity, "burning");
            manager.Statuses.RemoveStatus(entity, "burning");

            // Written as one string rather than four collection asserts so a failure names
            // which hook went quiet instead of only saying a count was wrong. That is how the
            // missing OnRemoveEffects wiring was found.
            Assert.AreEqual("apply=[burning] tick=[burning] refresh=[burning] remove=[burning]",
                $"apply=[{string.Join(",", onApply.Sources)}] " +
                $"tick=[{string.Join(",", onTick.Sources)}] " +
                $"refresh=[{string.Join(",", onRefresh.Sources)}] " +
                $"remove=[{string.Join(",", onRemove.Sources)}]");
        }

        [TestMethod]
        public void PeriodicEffect_RemembersItsSourceLongAfterApplication()
        {
            // The case a parameter passed at call time would miss: a periodic effect ticks
            // long after whoever applied it has returned.
            var manager = Victim(out var entity);
            var action = new RecordingAction();

            manager.Statuses.RegisterStatus(StatusBuilder.Create("rotting")
                .WithDuration(30f)
                .OnApply(EffectBuilder.Create("rot.dot")
                    // Outlasts the window on purpose. An effect whose duration ends on a tick
                    // boundary expires before it ticks, and that boundary is a separate
                    // question from the one this test is asking.
                    .Periodic(duration: 30f, tickInterval: 5f)
                    .WithAction(action)
                    .Build())
                .Build());

            manager.Statuses.ApplyStatus(entity, "rotting");

            for (var second = 0; second < 20; second++)
            {
                manager.Tick(1f);
            }

            Assert.AreEqual(4, action.Sources.Count, "expected four ticks over twenty seconds");
            CollectionAssert.AreEqual(new[] { "rotting", "rotting", "rotting", "rotting" }, action.Sources);
        }

        [TestMethod]
        public void OneActionScalesWithWhicheverStatusItRunsFor()
        {
            var manager = Victim(out var entity);
            var perStack = new DamagePerStackAction("Health", 2f);

            // The same action instance behind two different statuses. Neither was told a key.
            manager.Statuses.RegisterStatus(StatusBuilder.Create("bleeding")
                .WithDuration(60f).Stackable(5).WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("bleed.tick").Instant().WithAction(perStack).Build())
                .Build());

            manager.Statuses.RegisterStatus(StatusBuilder.Create("poisoned")
                .WithDuration(60f).Stackable(5).WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("poison.tick").Instant().WithAction(perStack).Build())
                .Build());

            manager.Statuses.ApplyStatus(entity, "bleeding");
            manager.Statuses.ApplyStatus(entity, "bleeding");
            manager.Statuses.ApplyStatus(entity, "bleeding");
            manager.Statuses.ApplyStatus(entity, "poisoned");

            manager.Tick(1f);

            // Three stacks of bleeding at 2 each, plus one of poison at 2.
            Assert.AreEqual(100f - 6f - 2f, entity.GetStat("Health").CurrentValue);
        }
    }
}
