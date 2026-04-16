using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Entities;
using Effectio.Modifiers;
using Effectio.Stats;

namespace Effectio.Tests.Stats
{
    [TestClass]
    public class ModifierExpirationTests
    {
        [TestMethod]
        public void TickModifiers_RemovesExpiredModifier_AndRecalculates()
        {
            var stat = new Stat("Damage", 50f);
            stat.AddModifier(new AdditiveModifier("buff", 10f, duration: 2f, sourceKey: "spell"));

            Assert.AreEqual(60f, stat.CurrentValue);

            bool expired = stat.TickModifiers(1f);
            Assert.IsFalse(expired);
            Assert.AreEqual(60f, stat.CurrentValue);

            expired = stat.TickModifiers(1.5f);
            Assert.IsTrue(expired);
            Assert.AreEqual(50f, stat.CurrentValue);
            Assert.AreEqual(0, stat.Modifiers.Count);
        }

        [TestMethod]
        public void TickModifiers_LeavesPermanentModifier()
        {
            var stat = new Stat("Damage", 50f);
            stat.AddModifier(new AdditiveModifier("perm", 5f, duration: -1f, sourceKey: "gear"));

            stat.TickModifiers(100f);

            Assert.AreEqual(55f, stat.CurrentValue);
            Assert.AreEqual(1, stat.Modifiers.Count);
        }
    }
}
