using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Entities;
using Effectio.Reactions;
using Effectio.Statuses;

namespace Effectio.Tests.Reactions
{
    /// <summary>
    /// Coverage for v1.1 reaction priority. Higher-priority reactions fire first
    /// and their consumed statuses are removed before lower-priority reactions
    /// re-evaluate, so a high-priority reaction can preempt overlapping
    /// low-priority ones. Reactions sharing a priority preserve v1.0
    /// "fire simultaneously" semantics.
    /// </summary>
    [TestClass]
    public class ReactionPriorityTests
    {
        private StatusEngine _statusEngine;
        private ReactionEngine _reactionEngine;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _statusEngine = new StatusEngine();
            _reactionEngine = new ReactionEngine(_statusEngine);
            _entity = new EffectioEntity("e1");

            _statusEngine.RegisterStatus(new Status("Burning"));
            _statusEngine.RegisterStatus(new Status("Wet"));
            _statusEngine.RegisterStatus(new Status("Charged"));
            _statusEngine.RegisterStatus(new Status("Vaporized"));
            _statusEngine.RegisterStatus(new Status("Apocalypsed"));
            _statusEngine.RegisterStatus(new Status("Shocked"));
        }

        [TestMethod]
        public void DefaultPriority_IsZero()
        {
            var r = new Reaction("r");
            Assert.AreEqual(0, r.Priority);

            var built = (IPrioritizedReaction)ReactionBuilder.Create("b").RequireStatus("Burning").Build();
            Assert.AreEqual(0, built.Priority);
        }

        [TestMethod]
        public void Builder_SetsPriority()
        {
            var r = (IPrioritizedReaction)ReactionBuilder.Create("r").RequireStatus("Burning").Priority(42).Build();
            Assert.AreEqual(42, r.Priority);
        }

        [TestMethod]
        public void EqualPriority_PreservesRegistrationOrder()
        {
            // The engine sorts reactions by priority desc on register; for equal
            // priorities, the sort must be stable (registration order preserved) so
            // the firing order stays deterministic.
            var order = new List<string>();
            _reactionEngine.OnReactionTriggered += (_, r) => order.Add(r.Key);

            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                _reactionEngine.RegisterReaction(ReactionBuilder.Create("R" + idx)
                    .RequireStatus("Wet")
                    .ConsumesStatuses(false)
                    .Priority(0)
                    .Build());
            }

            _statusEngine.ApplyStatus(_entity, "Wet");
            _reactionEngine.CheckReactions(_entity);

            // Each reaction fires once in registration order. The chain loop ends after
            // pass 1 because no new statuses appeared (these reactions have no results).
            CollectionAssert.AreEqual(new[] { "R0", "R1", "R2", "R3", "R4" }, order);
        }

        [TestMethod]
        public void Reaction_SupportsNegativePriority()
        {
            var r = (IPrioritizedReaction)ReactionBuilder.Create("r").RequireStatus("Burning").Priority(-100).Build();
            Assert.AreEqual(-100, r.Priority);
        }

        [TestMethod]
        public void HigherPriority_FiresAndPreemptsLowerPriority()
        {
            // Apocalypse (priority 100) requires Burning + Wet, applies "Apocalypsed".
            // Vaporize (priority 0) requires Burning + Wet, applies "Vaporized".
            // Both match. Higher priority fires first, consumes both statuses,
            // Vaporize then has nothing to match against and does NOT fire.
            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .ApplyStatus("Vaporized")
                .Build());

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Apocalypse")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .Priority(100)
                .ApplyStatus("Apocalypsed")
                .Build());

            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Apocalypsed"), "Higher-priority reaction should fire.");
            Assert.IsFalse(_entity.HasStatus("Vaporized"), "Lower-priority reaction should be preempted.");
            Assert.IsFalse(_entity.HasStatus("Burning"));
            Assert.IsFalse(_entity.HasStatus("Wet"));
        }

        [TestMethod]
        public void HigherPriority_FiresAndPreemptsRegardlessOfRegistrationOrder()
        {
            // Same as above but register the high-priority reaction LAST to confirm
            // priority - not registration order - drives execution.
            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Apocalypse")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .Priority(100)
                .ApplyStatus("Apocalypsed")
                .Build());

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .ApplyStatus("Vaporized")
                .Build());

            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Apocalypsed"));
            Assert.IsFalse(_entity.HasStatus("Vaporized"));
        }

        [TestMethod]
        public void EqualPriority_FiresSimultaneously_AgainstPreConsumeState()
        {
            // Both reactions have default priority 0 and require Wet. Without simultaneity,
            // the first would consume Wet and the second would not match. With simultaneity,
            // both fire because both see Wet present at the start of the tier.
            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .ApplyStatus("Vaporized")
                .Build());

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Shock")
                .RequireStatuses("Wet", "Charged")
                .ConsumesStatuses()
                .ApplyStatus("Shocked")
                .Build());

            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _statusEngine.ApplyStatus(_entity, "Charged");
            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Vaporized"));
            Assert.IsTrue(_entity.HasStatus("Shocked"));
            Assert.IsFalse(_entity.HasStatus("Burning"));
            Assert.IsFalse(_entity.HasStatus("Wet"));
            Assert.IsFalse(_entity.HasStatus("Charged"));
        }

        [TestMethod]
        public void NonOverlappingHighPriority_DoesNotPreemptUnrelatedLowPriority()
        {
            // High-priority reaction requires Burning. Low-priority reaction requires
            // Wet + Charged. They share no statuses. Both should fire (in priority order),
            // because high-priority's consumes do not affect low-priority's required set.
            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Shock")
                .RequireStatuses("Wet", "Charged")
                .ConsumesStatuses()
                .ApplyStatus("Shocked")
                .Build());

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Boom")
                .RequireStatus("Burning")
                .ConsumesStatuses()
                .Priority(100)
                .ApplyStatus("Vaporized")
                .Build());

            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _statusEngine.ApplyStatus(_entity, "Charged");
            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Vaporized"), "High-priority Boom should fire.");
            Assert.IsTrue(_entity.HasStatus("Shocked"), "Low-priority Shock should also fire (no overlap).");
        }

        [TestMethod]
        public void FiringOrder_IsHighPriorityFirst()
        {
            // Each reaction has its own required status so they don't preempt each other;
            // all three should fire in one pass, in priority order. ConsumesStatuses keeps
            // the chain loop from re-firing them.
            var order = new List<string>();
            _reactionEngine.OnReactionTriggered += (_, r) => order.Add(r.Key);

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Low")
                .RequireStatus("Wet")
                .ConsumesStatuses()
                .Priority(0)
                .ApplyStatus("Shocked")
                .Build());

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("Mid")
                .RequireStatus("Charged")
                .ConsumesStatuses()
                .Priority(50)
                .ApplyStatus("Vaporized")
                .Build());

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("High")
                .RequireStatus("Burning")
                .ConsumesStatuses()
                .Priority(100)
                .ApplyStatus("Apocalypsed")
                .Build());

            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Charged");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _reactionEngine.CheckReactions(_entity);

            CollectionAssert.AreEqual(new[] { "High", "Mid", "Low" }, order);
        }

        // -------- v1.0 backward-compatibility regression tests --------

        [TestMethod]
        public void V10Ctor_StillWorksAndDefaultsPriorityToZero()
        {
            // Calls the original 5-parameter Reaction ctor (no priority arg). This is
            // the IL signature pre-built v1.0 consumers reference; it must keep working.
            var r = new Reaction(
                "legacy",
                requiredStatusKeys: new[] { "Wet" },
                requiredTags: new string[0],
                consumesStatuses: false,
                results: new IReactionResult[] { new ApplyStatusResult("Shocked") });

            Assert.AreEqual(0, r.Priority);

            _statusEngine.RegisterStatus(new Status("Shocked"));
            _reactionEngine.RegisterReaction(r);
            _statusEngine.ApplyStatus(_entity, "Wet");
            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Shocked"));
        }

        /// <summary>
        /// Minimal external <see cref="IReaction"/> implementation that does NOT
        /// implement <see cref="IPrioritizedReaction"/>. Mirrors what a v1.0 consumer
        /// might have written by hand against the original interface. The engine must
        /// treat this as priority 0 and still fire it correctly.
        /// </summary>
        private sealed class LegacyExternalReaction : IReaction
        {
            public string Key { get; }
            public string[] RequiredStatusKeys { get; }
            public string[] RequiredTags => new string[0];
            public bool ConsumesStatuses => true;
            public IReactionResult[] Results { get; }

            public LegacyExternalReaction(string key, string requiredStatus, IReactionResult result)
            {
                Key = key;
                RequiredStatusKeys = new[] { requiredStatus };
                Results = new[] { result };
            }
        }

        [TestMethod]
        public void ExternalIReactionWithoutPriority_TreatedAsTierZero()
        {
            // A high-priority IPrioritizedReaction and a legacy external IReaction (no
            // IPrioritizedReaction implementation). The high-priority one fires first,
            // the legacy one fires after as part of tier 0.
            var order = new List<string>();
            _reactionEngine.OnReactionTriggered += (_, r) => order.Add(r.Key);

            _reactionEngine.RegisterReaction(new LegacyExternalReaction(
                "Legacy",
                requiredStatus: "Charged",
                result: new ApplyStatusResult("Shocked")));

            _reactionEngine.RegisterReaction(ReactionBuilder.Create("High")
                .RequireStatus("Burning")
                .ConsumesStatuses()
                .Priority(100)
                .ApplyStatus("Apocalypsed")
                .Build());

            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Charged");
            _reactionEngine.CheckReactions(_entity);

            // High (priority 100) fires before Legacy (priority 0 by default).
            CollectionAssert.AreEqual(new[] { "High", "Legacy" }, order);
            Assert.IsTrue(_entity.HasStatus("Apocalypsed"));
            Assert.IsTrue(_entity.HasStatus("Shocked"));
        }
    }
}
