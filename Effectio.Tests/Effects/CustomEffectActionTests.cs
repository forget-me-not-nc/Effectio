using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Effects.Actions;
using Effectio.Modifiers;
using Effectio.Stats;

namespace Effectio.Tests.Effects
{
    [TestClass]
    public class CustomEffectActionTests
    {
        /// <summary>Test action that doubles a stat on execute and halves it on undo.</summary>
        private sealed class DoubleStatAction : IEffectAction
        {
            private readonly string _stat;
            public int ExecuteCount { get; private set; }
            public int UndoCount { get; private set; }

            public DoubleStatAction(string stat) { _stat = stat; }

            public void Execute(in EffectActionContext ctx)
            {
                ExecuteCount++;
                var s = ctx.Entity.GetStat(_stat);
                s.BaseValue *= 2f;
                s.Recalculate();
            }

            public void Undo(in EffectActionContext ctx)
            {
                UndoCount++;
                var s = ctx.Entity.GetStat(_stat);
                s.BaseValue /= 2f;
                s.Recalculate();
            }
        }

        [TestMethod]
        public void CustomAction_InstantEffect_RunsExecute()
        {
            var manager = new EffectioManager();
            var p = manager.CreateEntity("p");
            p.AddStat(new Stat("Damage", 10f));

            var action = new DoubleStatAction("Damage");
            var effect = new Effect("doubler", EffectType.Instant, action);

            manager.Effects.ApplyEffect(p, effect);

            Assert.AreEqual(1, action.ExecuteCount);
            Assert.AreEqual(20f, p.GetStat("Damage").CurrentValue);
            Assert.AreEqual(EffectActionType.Custom, effect.ActionType);
            Assert.AreSame(action, effect.Action);
        }

        [TestMethod]
        public void CustomAction_AuraEffect_UndoesOnExpiration()
        {
            var manager = new EffectioManager();
            var p = manager.CreateEntity("p");
            p.AddStat(new Stat("Damage", 10f));

            var action = new DoubleStatAction("Damage");
            var aura = new Effect("aura_doubler", EffectType.Aura, action, duration: 1f);

            manager.Effects.ApplyEffect(p, aura);
            Assert.AreEqual(20f, p.GetStat("Damage").CurrentValue);

            manager.Tick(2f); // expire

            Assert.AreEqual(1, action.ExecuteCount);
            Assert.AreEqual(1, action.UndoCount);
            Assert.AreEqual(10f, p.GetStat("Damage").CurrentValue);
        }

        [TestMethod]
        public void EffectBuilder_WithAction_UsesCustomAction()
        {
            var action = new DoubleStatAction("Damage");

            var effect = EffectBuilder.Create("b")
                .Aura(duration: 5f)
                .WithAction(action)
                .Build();

            Assert.AreEqual(EffectType.Aura, effect.EffectType);
            Assert.AreEqual(EffectActionType.Custom, effect.ActionType);
            Assert.AreSame(action, effect.Action);
        }

        [TestMethod]
        public void ApplyModifierAction_WithFactory_AttachesMultiplicativeModifier()
        {
            var manager = new EffectioManager();
            var p = manager.CreateEntity("p");
            p.AddStat(new Stat("Damage", 10f));

            var effect = EffectBuilder.Create("rage")
                .Timed(duration: 5f)
                .ApplyModifier("Damage", e => new MultiplicativeModifier(e.Key + "_mod", 2f, e.Duration, e.Key))
                .Build();

            manager.Effects.ApplyEffect(p, effect);

            Assert.AreEqual(20f, p.GetStat("Damage").CurrentValue);
            Assert.AreEqual(1, p.GetStat("Damage").Modifiers.Count);
            Assert.IsInstanceOfType(p.GetStat("Damage").Modifiers[0], typeof(MultiplicativeModifier));
        }

        [TestMethod]
        public void ApplyModifierAction_WithFactory_ProducesFreshModifierPerExecute()
        {
            // Periodic effect: each tick should add a new modifier instance (separate RemainingTime).
            var manager = new EffectioManager();
            var p = manager.CreateEntity("p");
            p.AddStat(new Stat("Damage", 10f));

            int factoryCalls = 0;
            var effect = EffectBuilder.Create("pulse")
                .Periodic(duration: 3f, tickInterval: 1f)
                .ApplyModifier("Damage", e =>
                {
                    factoryCalls++;
                    return new AdditiveModifier(e.Key + "_mod_" + factoryCalls, 1f, 2f, e.Key);
                })
                .Build();

            manager.Effects.ApplyEffect(p, effect);

            manager.Tick(1f);
            manager.Tick(1f);

            Assert.AreEqual(2, factoryCalls);
            Assert.AreEqual(2, p.GetStat("Damage").Modifiers.Count);
        }
    }
}
