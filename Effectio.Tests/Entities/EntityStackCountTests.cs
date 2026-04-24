using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Core;
using Effectio.Entities;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Entities
{
    /// <summary>
    /// Coverage for the v1.1 <see cref="IEffectioEntity.GetStatusStackCount"/> ergonomic
    /// shortcut: returns the same number as <c>manager.Statuses.GetStacks(entity, key)</c>
    /// when the entity was created through <see cref="EffectioManager.CreateEntity"/>;
    /// returns 0 (documented fallback) when the entity was constructed manually
    /// without a status-engine reference.
    /// </summary>
    [TestClass]
    public class EntityStackCountTests
    {
        private EffectioManager _manager;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
            _manager.Statuses.RegisterStatus(new Status("Burning", duration: -1f, maxStacks: 5));
            _entity = _manager.CreateEntity("p");
        }

        [TestMethod]
        public void GetStatusStackCount_ReturnsZero_ForAbsentStatus()
        {
            Assert.AreEqual(0, _entity.GetStatusStackCount("Burning"));
            Assert.AreEqual(0, _entity.GetStatusStackCount("NeverHeardOfIt"));
        }

        [TestMethod]
        public void GetStatusStackCount_ReturnsCount_AfterApplyStatus()
        {
            _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(1, _entity.GetStatusStackCount("Burning"));

            _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(3, _entity.GetStatusStackCount("Burning"));
        }

        [TestMethod]
        public void GetStatusStackCount_MatchesStatusEngineGetStacks_Always()
        {
            // The shortcut MUST agree with the underlying engine query at every step.
            for (int i = 0; i < 5; i++)
            {
                _manager.Statuses.ApplyStatus(_entity, "Burning");
                Assert.AreEqual(
                    _manager.Statuses.GetStacks(_entity, "Burning"),
                    _entity.GetStatusStackCount("Burning"),
                    $"Disagreement after {i + 1} applications.");
            }
        }

        [TestMethod]
        public void GetStatusStackCount_ReturnsZero_AfterFullRemoval()
        {
            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.RemoveStatus(_entity, "Burning");

            Assert.AreEqual(0, _entity.GetStatusStackCount("Burning"));
        }

        [TestMethod]
        public void GetStatusStackCount_ReflectsPartialDecrement_FromRemoveStacks()
        {
            for (int i = 0; i < 4; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            ((IStackOperations)_manager.Statuses).RemoveStacks(_entity, "Burning", 2);

            Assert.AreEqual(2, _entity.GetStatusStackCount("Burning"));
        }

        // -------- Manual construction fallback --------

        [TestMethod]
        public void ManuallyConstructedEntity_NoEngineRef_ReturnsZero()
        {
            // v1.0-style construction: caller did not go through EffectioManager.CreateEntity
            // and did not pass a status engine. Documented behaviour: 0 for every key.
            var manual = new EffectioEntity("manual");

            Assert.AreEqual(0, manual.GetStatusStackCount("Burning"));
            Assert.AreEqual(0, manual.GetStatusStackCount("Anything"));
        }

        [TestMethod]
        public void ManuallyConstructedEntity_WithEngineRef_QueriesCorrectly()
        {
            // Caller constructs entity manually but passes the engine ref directly.
            var statusEngine = new StatusEngine();
            statusEngine.RegisterStatus(new Status("Slow", duration: -1f, maxStacks: 3));
            var manual = new EffectioEntity("manual", statusEngine);

            statusEngine.ApplyStatus(manual, "Slow");
            statusEngine.ApplyStatus(manual, "Slow");

            Assert.AreEqual(2, manual.GetStatusStackCount("Slow"));
        }

        // -------- External IEffectioEntity backward-compat --------

        /// <summary>
        /// External implementation of <see cref="IEffectioEntity"/> demonstrating that
        /// third-party entity types adding <see cref="IEffectioEntity.GetStatusStackCount"/>
        /// keep full control of their stack-count source.
        /// </summary>
        private sealed class ExternalEntity : IEffectioEntity
        {
            public string Id => "external";
            public System.Collections.Generic.IReadOnlyCollection<string> StatKeys => System.Array.Empty<string>();
            public System.Collections.Generic.IReadOnlyCollection<string> ActiveStatusKeys => System.Array.Empty<string>();

            public IStat GetStat(string key) => throw new System.NotImplementedException();
            public bool TryGetStat(string key, out IStat stat) { stat = null; return false; }
            public bool HasStat(string key) => false;
            public void AddStat(IStat stat) { }
            public void TickStatModifiers(float deltaTime) { }
            public void AddStatus(string statusKey) { }
            public void RemoveStatus(string statusKey) { }
            public bool HasStatus(string statusKey) => false;
            public void CopyStatusKeysTo(System.Collections.Generic.ICollection<string> dest) { }

            // External impl returns a hard-coded value to demonstrate the engine
            // does not enforce any specific source for stack counts.
            public int GetStatusStackCount(string statusKey) => statusKey == "Magic" ? 42 : 0;
        }

        [TestMethod]
        public void ExternalEntity_OwnsItsStackSource()
        {
            var ext = new ExternalEntity();
            Assert.AreEqual(42, ext.GetStatusStackCount("Magic"));
            Assert.AreEqual(0, ext.GetStatusStackCount("Other"));
        }
    }
}

