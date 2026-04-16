using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Effects;
using Effectio.Entities;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Effects
{
    [TestClass]
    public class EffectsEngineTests
    {
        private StatusEngine _statusEngine;
        private EffectsEngine _effectsEngine;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _statusEngine = new StatusEngine();
            _effectsEngine = new EffectsEngine(_statusEngine);
            _entity = new EffectioEntity("player1");
            _entity.AddStat(new Stat("Health", 100f, 0f, 500f));
            _entity.AddStat(new Stat("Damage", 50f, 0f, 1000f));
        }

        [TestMethod]
        public void InstantEffect_AdjustsStat()
        {
            var effect = new Effect("heal", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: 25f);

            _effectsEngine.ApplyEffect(_entity, effect);

            Assert.AreEqual(125f, _entity.GetStat("Health").CurrentValue);
        }

        [TestMethod]
        public void InstantEffect_AppliesStatus()
        {
            _statusEngine.RegisterStatus(new Status("Burning"));
            var effect = new Effect("ignite", EffectType.Instant, EffectActionType.ApplyStatus, "Burning");

            _effectsEngine.ApplyEffect(_entity, effect);

            Assert.IsTrue(_entity.HasStatus("Burning"));
        }

        [TestMethod]
        public void InstantEffect_RemovesStatus()
        {
            _statusEngine.RegisterStatus(new Status("Burning"));
            _statusEngine.ApplyStatus(_entity, "Burning");

            var effect = new Effect("cleanse", EffectType.Instant, EffectActionType.RemoveStatus, "Burning");
            _effectsEngine.ApplyEffect(_entity, effect);

            Assert.IsFalse(_entity.HasStatus("Burning"));
        }

        [TestMethod]
        public void Effect_FiresOnEffectAppliedEvent()
        {
            IEffect appliedEffect = null;
            _effectsEngine.OnEffectApplied += (e, eff) => appliedEffect = eff;

            var effect = new Effect("heal", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: 10f);
            _effectsEngine.ApplyEffect(_entity, effect);

            Assert.IsNotNull(appliedEffect);
            Assert.AreEqual("heal", appliedEffect.Key);
        }

        [TestMethod]
        public void InstantEffect_NegativeValue_ReducesStat()
        {
            var effect = new Effect("damage", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: -30f);

            _effectsEngine.ApplyEffect(_entity, effect);

            Assert.AreEqual(70f, _entity.GetStat("Health").CurrentValue);
        }
    }
}
