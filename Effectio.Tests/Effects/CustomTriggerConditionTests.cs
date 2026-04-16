using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Effects.Triggers;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Effects
{
    [TestClass]
    public class CustomTriggerConditionTests
    {
        private EffectioManager _manager;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
        }

        [TestMethod]
        public void AndTrigger_FiresOnlyWhenBothChildrenSatisfied()
        {
            _manager.Statuses.RegisterStatus(new Status("Rage"));
            var p = _manager.CreateEntity("p");
            p.AddStat(new Stat("Health", 100f, 0f, 100f));
            p.AddStat(new Stat("Shield", 0f, 0f, 500f));

            var effect = EffectBuilder.Create("Combo")
                .Triggered(duration: 10f)
                .AdjustStat("Shield", 50f)
                .When(new AndTrigger(
                    new StatBelowTrigger("Health", 30f),
                    new HasStatusTrigger("Rage")))
                .Build();

            _manager.Effects.ApplyEffect(p, effect);

            // Only HP low — not satisfied yet
            p.GetStat("Health").BaseValue = 20f;
            p.GetStat("Health").Recalculate();
            _manager.Tick(0.1f);
            Assert.AreEqual(0f, p.GetStat("Shield").CurrentValue);

            // Add Rage — now both children satisfied
            _manager.Statuses.ApplyStatus(p, "Rage");
            _manager.Tick(0.1f);
            Assert.AreEqual(50f, p.GetStat("Shield").CurrentValue);
        }

        [TestMethod]
        public void OrTrigger_FiresWhenAnyChildSatisfied()
        {
            _manager.Statuses.RegisterStatus(new Status("Panic"));
            var p = _manager.CreateEntity("p");
            p.AddStat(new Stat("Health", 100f, 0f, 100f));
            p.AddStat(new Stat("Speed", 1f, 0f, 10f));

            var effect = EffectBuilder.Create("Flee")
                .Triggered(duration: 10f)
                .AdjustStat("Speed", 2f)
                .When(new OrTrigger(
                    new StatBelowTrigger("Health", 30f),
                    new HasStatusTrigger("Panic")))
                .Build();

            _manager.Effects.ApplyEffect(p, effect);
            _manager.Tick(0.1f);
            Assert.AreEqual(1f, p.GetStat("Speed").CurrentValue);

            // Panic alone is enough
            _manager.Statuses.ApplyStatus(p, "Panic");
            _manager.Tick(0.1f);
            Assert.AreEqual(3f, p.GetStat("Speed").CurrentValue);
        }

        [TestMethod]
        public void NotTrigger_InvertsChild()
        {
            _manager.Statuses.RegisterStatus(new Status("Shielded"));
            var p = _manager.CreateEntity("p");
            p.AddStat(new Stat("Damage", 0f, 0f, 100f));

            var effect = EffectBuilder.Create("Exposed")
                .Triggered(duration: 10f)
                .AdjustStat("Damage", 10f)
                .When(new NotTrigger(new HasStatusTrigger("Shielded")))
                .Build();

            _manager.Effects.ApplyEffect(p, effect);

            _manager.Statuses.ApplyStatus(p, "Shielded");
            _manager.Tick(0.1f);
            Assert.AreEqual(0f, p.GetStat("Damage").CurrentValue, "Shielded -> trigger not satisfied");

            _manager.Statuses.RemoveStatus(p, "Shielded");
            _manager.Tick(0.1f);
            Assert.AreEqual(10f, p.GetStat("Damage").CurrentValue);
        }

        [TestMethod]
        public void LegacyTriggerEnum_StillProducesCorrectCondition()
        {
            var e = EffectBuilder.Create("x")
                .Triggered(10f)
                .AdjustStat("Health", 1f)
                .WhenStatBelow("Health", 5f)
                .Build();

            Assert.IsInstanceOfType(e.Trigger, typeof(StatBelowTrigger));
        }

        [TestMethod]
        public void NoTrigger_DefaultsToNeverTrigger()
        {
            var e = EffectBuilder.Create("x").Instant().AdjustStat("Health", 1f).Build();

            Assert.AreSame(NeverTrigger.Instance, e.Trigger);
        }
    }
}
