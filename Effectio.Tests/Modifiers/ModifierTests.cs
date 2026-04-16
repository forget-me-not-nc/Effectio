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
            var mod = new Modifier("test", ModifierType.Additive, 10f);

            Assert.AreEqual(-1f, mod.Duration);
            Assert.AreEqual(-1f, mod.RemainingTime);
            Assert.IsFalse(mod.IsExpired);
        }

        [TestMethod]
        public void Modifier_ExpiresWhenRemainingTimeZero()
        {
            var mod = new Modifier("test", ModifierType.Additive, 10f, duration: 5f);

            Assert.IsFalse(mod.IsExpired);

            mod.RemainingTime = 0f;
            Assert.IsTrue(mod.IsExpired);
        }

        [TestMethod]
        public void Modifier_TracksSourceKey()
        {
            var mod = new Modifier("dmg_boost", ModifierType.Multiplicative, 1.5f, sourceKey: "power_buff");

            Assert.AreEqual("power_buff", mod.SourceKey);
        }
    }
}
