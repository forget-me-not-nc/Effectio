using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Tests.Statuses
{
    [TestClass]
    public class StatusEngineTests
    {
        private StatusEngine _engine;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _engine = new StatusEngine();
            _entity = new EffectioEntity("player1");
        }

        [TestMethod]
        public void ApplyStatus_AddsToEntity()
        {
            _engine.RegisterStatus(new Status("Burning", tags: new[] { "Fire" }, duration: 5f));

            _engine.ApplyStatus(_entity, "Burning");

            Assert.IsTrue(_entity.HasStatus("Burning"));
            Assert.IsTrue(_engine.HasStatus(_entity, "Burning"));
        }

        [TestMethod]
        public void RemoveStatus_RemovesFromEntity()
        {
            _engine.RegisterStatus(new Status("Burning"));
            _engine.ApplyStatus(_entity, "Burning");

            _engine.RemoveStatus(_entity, "Burning");

            Assert.IsFalse(_entity.HasStatus("Burning"));
        }

        [TestMethod]
        public void ApplyStatus_FiresEvent()
        {
            _engine.RegisterStatus(new Status("Wet"));
            string appliedKey = null;
            _engine.OnStatusApplied += (e, key) => appliedKey = key;

            _engine.ApplyStatus(_entity, "Wet");

            Assert.AreEqual("Wet", appliedKey);
        }

        [TestMethod]
        public void Stacking_RespectsMaxStacks()
        {
            _engine.RegisterStatus(new Status("Poison", maxStacks: 3));

            _engine.ApplyStatus(_entity, "Poison");
            Assert.AreEqual(1, _engine.GetStacks(_entity, "Poison"));

            _engine.ApplyStatus(_entity, "Poison");
            Assert.AreEqual(2, _engine.GetStacks(_entity, "Poison"));

            _engine.ApplyStatus(_entity, "Poison");
            Assert.AreEqual(3, _engine.GetStacks(_entity, "Poison"));

            // Should not exceed max
            _engine.ApplyStatus(_entity, "Poison");
            Assert.AreEqual(3, _engine.GetStacks(_entity, "Poison"));
        }

        [TestMethod]
        public void UnregisteredStatus_AppliesAsTag()
        {
            _engine.ApplyStatus(_entity, "CustomTag");

            Assert.IsTrue(_entity.HasStatus("CustomTag"));
        }
    }
}
