using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Stats;
using Effectio.Modifiers;

namespace Effectio.Tests.Stats
{
    [TestClass]
    public class StatsTests
    {
        [TestMethod]
        public void Stat_InitializesWithBaseValue()
        {
            var stat = new Stat("Health", 100f, 0f, 200f);

            Assert.AreEqual("Health", stat.Key);
            Assert.AreEqual(100f, stat.BaseValue);
            Assert.AreEqual(100f, stat.CurrentValue);
            Assert.AreEqual(0f, stat.Min);
            Assert.AreEqual(200f, stat.Max);
        }

        [TestMethod]
        public void Stat_ClampsToMinMax()
        {
            var stat = new Stat("Health", 300f, 0f, 200f);
            Assert.AreEqual(200f, stat.CurrentValue);

            var stat2 = new Stat("Health", -50f, 0f, 200f);
            Assert.AreEqual(0f, stat2.CurrentValue);
        }

        [TestMethod]
        public void AdditiveModifier_IncreasesValue()
        {
            var stat = new Stat("Health", 100f, 0f, 500f);
            var modifier = new AdditiveModifier("bonus_hp", 50f);

            stat.AddModifier(modifier);

            Assert.AreEqual(150f, stat.CurrentValue);
        }

        [TestMethod]
        public void MultiplicativeModifier_MultipliesValue()
        {
            var stat = new Stat("Damage", 50f, 0f, 1000f);
            var modifier = new MultiplicativeModifier("dmg_mult", 2f);

            stat.AddModifier(modifier);

            Assert.AreEqual(100f, stat.CurrentValue);
        }

        [TestMethod]
        public void ModifierPipeline_AdditiveBeforeMultiplicative()
        {
            var stat = new Stat("Damage", 100f, 0f, 10000f);
            // Insert in reverse to prove sorted-insert correctness.
            stat.AddModifier(new MultiplicativeModifier("double", 2f));
            stat.AddModifier(new AdditiveModifier("flat_bonus", 50f));

            // (100 + 50) * 2 = 300
            Assert.AreEqual(300f, stat.CurrentValue);
        }

        [TestMethod]
        public void CapAdjustment_IncreasesMaximum()
        {
            var stat = new Stat("Health", 100f, 0f, 100f);
            stat.AddModifier(new CapAdjustmentModifier("hp_cap", 50f));
            stat.AddModifier(new AdditiveModifier("flat_hp", 30f));

            // 100 + 30 = 130, max is 100 + 50 = 150, so 130
            Assert.AreEqual(130f, stat.CurrentValue);
        }

        [TestMethod]
        public void RemoveModifier_RestoresValue()
        {
            var stat = new Stat("Health", 100f, 0f, 500f);
            stat.AddModifier(new AdditiveModifier("bonus", 50f));
            Assert.AreEqual(150f, stat.CurrentValue);

            stat.RemoveModifier("bonus");
            Assert.AreEqual(100f, stat.CurrentValue);
        }

        [TestMethod]
        public void RemoveModifiersFromSource_RemovesAllFromSource()
        {
            var stat = new Stat("Health", 100f, 0f, 500f);
            stat.AddModifier(new AdditiveModifier("mod1", 20f, sourceKey: "buff_A"));
            stat.AddModifier(new AdditiveModifier("mod2", 30f, sourceKey: "buff_A"));
            stat.AddModifier(new AdditiveModifier("mod3", 10f, sourceKey: "buff_B"));

            Assert.AreEqual(160f, stat.CurrentValue);

            stat.RemoveModifiersFromSource("buff_A");
            Assert.AreEqual(110f, stat.CurrentValue);
        }

        [TestMethod]
        public void OnValueChanged_FiresOnModification()
        {
            var stat = new Stat("Health", 100f, 0f, 500f);
            float oldVal = 0, newVal = 0;
            stat.OnValueChanged += (s, o, n) => { oldVal = o; newVal = n; };

            stat.AddModifier(new AdditiveModifier("bonus", 25f));

            Assert.AreEqual(100f, oldVal);
            Assert.AreEqual(125f, newVal);
        }
    }
}
