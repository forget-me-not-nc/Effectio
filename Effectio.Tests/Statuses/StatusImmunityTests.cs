using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Tests.Statuses
{
    [TestClass]
    public class StatusImmunityTests
    {
        private StatusEngine _engine;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _engine = new StatusEngine();
            _engine.RegisterStatus(new Status("Poison"));
            _entity = new EffectioEntity("p");
        }

        [TestMethod]
        public void Immunity_BlocksStatusApplication()
        {
            _engine.GrantImmunity(_entity, "Poison");
            _engine.ApplyStatus(_entity, "Poison");

            Assert.IsFalse(_entity.HasStatus("Poison"));
        }

        [TestMethod]
        public void Immunity_FiresOnStatusBlockedEvent()
        {
            string blockedKey = null;
            _engine.OnStatusBlocked += (e, key) => blockedKey = key;

            _engine.GrantImmunity(_entity, "Poison");
            _engine.ApplyStatus(_entity, "Poison");

            Assert.AreEqual("Poison", blockedKey);
        }

        [TestMethod]
        public void Revoke_AllowsStatusApplicationAgain()
        {
            _engine.GrantImmunity(_entity, "Poison");
            _engine.RevokeImmunity(_entity, "Poison");

            _engine.ApplyStatus(_entity, "Poison");

            Assert.IsTrue(_entity.HasStatus("Poison"));
        }

        [TestMethod]
        public void IsImmune_ReturnsCorrectValue()
        {
            Assert.IsFalse(_engine.IsImmune(_entity, "Poison"));
            _engine.GrantImmunity(_entity, "Poison");
            Assert.IsTrue(_engine.IsImmune(_entity, "Poison"));
        }
    }
}
