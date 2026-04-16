using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Modifiers;

namespace Effectio.Tests.Modifiers
{
    [TestClass]
    public class ModifierTests
    {
        [TestMethod]
        public void Modifier_PermanentByDefault()
        {
            var mod = new AdditiveModifier("test", 10f);

            Assert.AreEqual(-1f, mod.Duration);
            Assert.AreEqual(-1f, mod.RemainingTime);
            Assert.IsFalse(mod.IsExpired);
        }

        [TestMethod]
        public void Modifier_ExpiresWhenRemainingTimeZero()
        {
            var mod = new AdditiveModifier("test", 10f, duration: 5f);

            Assert.IsFalse(mod.IsExpired);

            mod.RemainingTime = 0f;
            Assert.IsTrue(mod.IsExpired);
        }

        [TestMethod]
        public void Modifier_TracksSourceKey()
        {
            var mod = new MultiplicativeModifier("dmg_boost", 1.5f, sourceKey: "power_buff");

            Assert.AreEqual("power_buff", mod.SourceKey);
        }

        [TestMethod]
        public void Modifier_Priority_OrdersKinds()
        {
            Assert.IsTrue(new AdditiveModifier("a", 1f).Priority < new MultiplicativeModifier("m", 1f).Priority);
            Assert.IsTrue(new MultiplicativeModifier("m", 1f).Priority < new CapAdjustmentModifier("c", 1f).Priority);
        }

        [TestMethod]
        public void AdditiveModifier_ApplyAddsToValue()
        {
            var ctx = new StatCalculationContext { Value = 10f };
            new AdditiveModifier("a", 5f).Apply(ref ctx);
            Assert.AreEqual(15f, ctx.Value);
        }

        [TestMethod]
        public void MultiplicativeModifier_ApplyMultipliesValue()
        {
            var ctx = new StatCalculationContext { Value = 10f };
            new MultiplicativeModifier("m", 2f).Apply(ref ctx);
            Assert.AreEqual(20f, ctx.Value);
        }

        [TestMethod]
        public void CapAdjustmentModifier_ApplyExtendsMax()
        {
            var ctx = new StatCalculationContext { Value = 10f, EffectiveMax = 100f };
            new CapAdjustmentModifier("c", 50f).Apply(ref ctx);
            Assert.AreEqual(150f, ctx.EffectiveMax);
            Assert.AreEqual(10f, ctx.Value);
        }
    }
}

