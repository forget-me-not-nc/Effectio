using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Effects
{
    [TestClass]
    public class AuraEffectTests
    {
        private EffectioManager _manager;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
        }

        [TestMethod]
        public void Aura_AdjustStat_AppliesImmediately()
        {
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Armor", 10f, 0f, 500f));

            var aura = new Effect("ArmorAura", EffectType.Aura, EffectActionType.AdjustStat, "Armor", value: 20f, duration: 5f);
            _manager.Effects.ApplyEffect(entity, aura);

            Assert.AreEqual(30f, entity.GetStat("Armor").CurrentValue);
        }

        [TestMethod]
        public void Aura_AdjustStat_UndoesOnManualRemoval()
        {
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Armor", 10f, 0f, 500f));

            var aura = new Effect("ArmorAura", EffectType.Aura, EffectActionType.AdjustStat, "Armor", value: 20f, duration: 5f);
            _manager.Effects.ApplyEffect(entity, aura);
            _manager.Effects.RemoveEffect(entity, "ArmorAura");

            Assert.AreEqual(10f, entity.GetStat("Armor").CurrentValue);
        }

        [TestMethod]
        public void Aura_AdjustStat_UndoesOnExpiration()
        {
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Armor", 10f, 0f, 500f));

            var aura = new Effect("ArmorAura", EffectType.Aura, EffectActionType.AdjustStat, "Armor", value: 20f, duration: 2f);
            _manager.Effects.ApplyEffect(entity, aura);
            Assert.AreEqual(30f, entity.GetStat("Armor").CurrentValue);

            _manager.Tick(3f); // expire

            Assert.AreEqual(10f, entity.GetStat("Armor").CurrentValue);
        }

        [TestMethod]
        public void Aura_ApplyModifier_RemovesModifiersOnExpiration()
        {
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Damage", 50f));

            var aura = new Effect("DmgAura", EffectType.Aura, EffectActionType.ApplyModifier, "Damage", value: 10f, duration: 1f);
            _manager.Effects.ApplyEffect(entity, aura);

            Assert.AreEqual(60f, entity.GetStat("Damage").CurrentValue);

            _manager.Tick(2f);

            Assert.AreEqual(50f, entity.GetStat("Damage").CurrentValue);
            Assert.AreEqual(0, entity.GetStat("Damage").Modifiers.Count);
        }
    }

    [TestClass]
    public class TriggeredEffectTests
    {
        private EffectioManager _manager;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
        }

        [TestMethod]
        public void Triggered_StatBelow_FiresOnlyWhenThresholdCrossed()
        {
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Health", 100f, 0f, 100f));
            entity.AddStat(new Stat("Shield", 0f, 0f, 500f));

            var trig = new Effect(
                "LowHpShield",
                EffectType.Triggered,
                EffectActionType.AdjustStat,
                "Shield",
                value: 50f,
                duration: 10f,
                triggerCondition: TriggerConditionType.StatBelow,
                triggerKey: "Health",
                triggerThreshold: 30f);

            _manager.Effects.ApplyEffect(entity, trig);
            _manager.Tick(0.1f);
            Assert.AreEqual(0f, entity.GetStat("Shield").CurrentValue, "should not trigger while HP high");

            // Drop HP below threshold
            entity.GetStat("Health").BaseValue = 20f;
            entity.GetStat("Health").Recalculate();

            _manager.Tick(0.1f);
            Assert.AreEqual(50f, entity.GetStat("Shield").CurrentValue, "should trigger once HP below 30");

            // Does not fire a second time
            _manager.Tick(0.1f);
            Assert.AreEqual(50f, entity.GetStat("Shield").CurrentValue);
        }

        [TestMethod]
        public void Triggered_HasStatus_FiresWhenStatusPresent()
        {
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Damage", 10f));
            _manager.Statuses.RegisterStatus(new Status("Rage"));

            var trig = new Effect(
                "RageBonus",
                EffectType.Triggered,
                EffectActionType.AdjustStat,
                "Damage",
                value: 15f,
                duration: 10f,
                triggerCondition: TriggerConditionType.HasStatus,
                triggerKey: "Rage");

            _manager.Effects.ApplyEffect(entity, trig);
            _manager.Tick(0.1f);
            Assert.AreEqual(10f, entity.GetStat("Damage").CurrentValue);

            _manager.Statuses.ApplyStatus(entity, "Rage");
            _manager.Tick(0.1f);

            Assert.AreEqual(25f, entity.GetStat("Damage").CurrentValue);
        }
    }
}
