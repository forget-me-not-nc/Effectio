using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Tests.Statuses
{
    /// <summary>
    /// Coverage for the v1.1 <see cref="IStackOperations"/> surface
    /// (<see cref="StatusEngine.RemoveStacks"/> — partial-stack decrement).
    /// </summary>
    [TestClass]
    public class StackOperationsTests
    {
        private StatusEngine _engine;
        private IStackOperations _ops;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _engine = new StatusEngine();
            _ops = _engine; // StatusEngine implements both
            _entity = new EffectioEntity("player1");
            _engine.RegisterStatus(new Status("Burning", maxStacks: 10));
        }

        [TestMethod]
        public void RemoveStacks_DecrementsCounter_BelowFullRemoval()
        {
            // Build up 5 stacks (ApplyStatus increments by 1 each time, capped at MaxStacks).
            for (int i = 0; i < 5; i++) _engine.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(5, _engine.GetStacks(_entity, "Burning"));

            _ops.RemoveStacks(_entity, "Burning", 2);

            Assert.AreEqual(3, _engine.GetStacks(_entity, "Burning"));
            Assert.IsTrue(_entity.HasStatus("Burning"), "Status should still be present after partial decrement.");
        }

        [TestMethod]
        public void RemoveStacks_RemovesStatusEntirely_WhenCountReachesZero()
        {
            for (int i = 0; i < 3; i++) _engine.ApplyStatus(_entity, "Burning");

            _ops.RemoveStacks(_entity, "Burning", 3);

            Assert.AreEqual(0, _engine.GetStacks(_entity, "Burning"));
            Assert.IsFalse(_entity.HasStatus("Burning"), "Status should be removed when stacks reach 0.");
        }

        [TestMethod]
        public void RemoveStacks_CountExceedingStacks_RemovesEntirely()
        {
            for (int i = 0; i < 2; i++) _engine.ApplyStatus(_entity, "Burning");

            _ops.RemoveStacks(_entity, "Burning", 100);

            Assert.AreEqual(0, _engine.GetStacks(_entity, "Burning"));
            Assert.IsFalse(_entity.HasStatus("Burning"));
        }

        [TestMethod]
        public void RemoveStacks_FiresOnStatusRemoved_OnlyOnFullRemoval()
        {
            int removedCount = 0;
            _engine.OnStatusRemoved += (_, _) => removedCount++;

            for (int i = 0; i < 3; i++) _engine.ApplyStatus(_entity, "Burning");

            _ops.RemoveStacks(_entity, "Burning", 1); // partial
            Assert.AreEqual(0, removedCount, "Partial decrement must not fire OnStatusRemoved.");

            _ops.RemoveStacks(_entity, "Burning", 2); // takes it to 0
            Assert.AreEqual(1, removedCount, "Full removal should fire OnStatusRemoved exactly once.");
        }

        [TestMethod]
        public void RemoveStacks_NonPositiveCount_IsNoOp()
        {
            for (int i = 0; i < 3; i++) _engine.ApplyStatus(_entity, "Burning");

            _ops.RemoveStacks(_entity, "Burning", 0);
            Assert.AreEqual(3, _engine.GetStacks(_entity, "Burning"));

            _ops.RemoveStacks(_entity, "Burning", -5);
            Assert.AreEqual(3, _engine.GetStacks(_entity, "Burning"));
        }

        [TestMethod]
        public void RemoveStacks_StatusNotPresent_IsNoOp()
        {
            // No Burning ever applied
            _ops.RemoveStacks(_entity, "Burning", 1);

            Assert.AreEqual(0, _engine.GetStacks(_entity, "Burning"));
            Assert.IsFalse(_entity.HasStatus("Burning"));
        }
    }
}
