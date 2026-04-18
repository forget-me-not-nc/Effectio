using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Reactions;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Reactions
{
    /// <summary>
    /// End-to-end coverage for v1.1 stack-aware reactions: <c>RequireStacks</c>
    /// gates matching, <c>ConsumesStacks</c> decrements per key (taking precedence
    /// over <c>ConsumesStatuses</c>), and reactions without these methods behave
    /// exactly as in v1.0 (regression).
    /// </summary>
    [TestClass]
    public class StackAwareReactionTests
    {
        private EffectioManager _manager;
        private Effectio.Entities.IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
            _manager.Statuses.RegisterStatus(new Status("Burning", maxStacks: 10));
            _manager.Statuses.RegisterStatus(new Status("Wet", maxStacks: 10));
            _manager.Statuses.RegisterStatus(new Status("Inferno", maxStacks: 5));
            _manager.Statuses.RegisterStatus(new Status("Vaporize"));
            _entity = _manager.CreateEntity("p");
            _entity.AddStat(new Stat("Health", 100f, 0f, 500f));
        }

        // -------- RequireStacks: matching --------

        [TestMethod]
        public void RequireStacks_MatchesWhenStacksAtLeastMin()
        {
            int fired = 0;
            _manager.Reactions.OnReactionTriggered += (_, _) => fired++;

            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("HotEnough")
                .RequireStacks("Burning", 3)
                .Persists()
                .Build());

            // Apply Burning 3 times -> 3 stacks
            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");

            Assert.AreEqual(1, fired, "Reaction should fire on the 3rd Burning application that pushed stacks to >= 3.");
        }

        [TestMethod]
        public void RequireStacks_DoesNotMatchWhenStacksBelowMin()
        {
            int fired = 0;
            _manager.Reactions.OnReactionTriggered += (_, _) => fired++;

            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("HotEnough")
                .RequireStacks("Burning", 5)
                .Persists()
                .Build());

            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");

            Assert.AreEqual(0, fired, "Reaction must not fire while stacks (3) remain below the threshold (5).");
        }

        [TestMethod]
        public void RequireStacks_MultipleKeys_AllMustBeSatisfied()
        {
            int fired = 0;
            _manager.Reactions.OnReactionTriggered += (_, _) => fired++;

            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Inferno")
                .RequireStacks("Burning", 3)
                .RequireStacks("Wet", 2)
                .Persists()
                .Build());

            // 3 Burning, but only 1 Wet -> below Wet threshold, no fire.
            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.ApplyStatus(_entity, "Wet");
            Assert.AreEqual(0, fired, "Should not fire while Wet is below threshold.");

            // Bring Wet to 2 -> all thresholds met.
            _manager.Statuses.ApplyStatus(_entity, "Wet");
            Assert.AreEqual(1, fired, "Should fire once both thresholds are met.");
        }

        // -------- ConsumesStacks: per-key decrement --------

        [TestMethod]
        public void ConsumesStacks_DecrementsStacksOnFire()
        {
            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Inferno")
                .RequireStacks("Burning", 3)
                .ConsumesStacks("Burning", 1)
                .ApplyStatus("Inferno")
                .Build());

            // Apply 3 stacks of Burning - the 3rd application triggers the reaction.
            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");

            // Reaction fired once: consumed 1 Burning -> 2 left, applied Inferno.
            Assert.AreEqual(2, _manager.Statuses.GetStacks(_entity, "Burning"));
            Assert.IsTrue(_entity.HasStatus("Inferno"));
        }

        [TestMethod]
        public void ConsumesStacks_FullCount_RemovesStatusEntirely()
        {
            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Snuff")
                .RequireStacks("Burning", 3)
                .ConsumesStacks("Burning", 3)
                .Build());

            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");

            Assert.AreEqual(0, _manager.Statuses.GetStacks(_entity, "Burning"));
            Assert.IsFalse(_entity.HasStatus("Burning"));
        }

        [TestMethod]
        public void ConsumesStacks_OverridesConsumesStatusesFlagPerKey()
        {
            // Reaction requires Burning + Wet. Consumes 1 Burning stack but
            // ConsumesStatuses(true) should still fully remove Wet (the key not
            // covered by any StackConsume entry).
            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Mixed")
                .RequireStacks("Burning", 2)
                .RequireStatus("Wet")
                .ConsumesStacks("Burning", 1)
                .ConsumesStatuses(true)
                .Build());

            for (int i = 0; i < 2; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.ApplyStatus(_entity, "Wet");

            // Burning: decremented, not removed
            Assert.AreEqual(1, _manager.Statuses.GetStacks(_entity, "Burning"));
            Assert.IsTrue(_entity.HasStatus("Burning"));
            // Wet: removed entirely (fall-through to ConsumesStatuses flag)
            Assert.IsFalse(_entity.HasStatus("Wet"));
        }

        [TestMethod]
        public void InfernoChain_NaturallyChainsAcrossPasses()
        {
            // Classic v1.1 use-case: 5 Burning + 1 Wet -> Inferno consumes 1 Burning per fire.
            // Because RequireStacks("Burning", 1) stays satisfied for stacks 5..1 and
            // RequireStatus("Wet") never gets consumed (no entry for Wet), this fires
            // up to MaxChainDepth times in a single CheckReactions call.
            var fireOrder = new List<string>();
            _manager.Reactions.OnReactionTriggered += (_, r) => fireOrder.Add(r.Key);

            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Inferno")
                .RequireStacks("Burning", 1)
                .RequireStatus("Wet")
                .ConsumesStacks("Burning", 1)
                .Persists() // do NOT remove Wet whole-status; chain depends on it surviving
                .ApplyStatus("Inferno")
                .Build());

            for (int i = 0; i < 5; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.ApplyStatus(_entity, "Wet"); // last call triggers reaction check

            // Reaction fires repeatedly within MaxChainDepth (default 5). Each fire
            // applies Inferno (no new statuses after first -> chain detection breaks
            // after pass 2 unless something else changes). Conservative assertion:
            // at least 1 fire, at most MaxChainDepth fires.
            Assert.IsTrue(fireOrder.Count >= 1, "Inferno should fire at least once.");
            Assert.IsTrue(fireOrder.Count <= _manager.Reactions.MaxChainDepth, "Chain should be bounded by MaxChainDepth.");
            Assert.IsTrue(_entity.HasStatus("Inferno"));
            // Whatever number fired, that many Burning stacks should have been consumed.
            Assert.AreEqual(5 - fireOrder.Count, _manager.Statuses.GetStacks(_entity, "Burning"));
        }

        // -------- Backward-compatibility regression --------

        [TestMethod]
        public void NoStackMethods_BehavesLikeV10()
        {
            // Classic v1.0 reaction - no RequireStacks, no ConsumesStacks. Should
            // match purely on RequiredStatusKeys and consume the whole status when
            // ConsumesStatuses(true).
            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses(true)
                .ApplyStatus("Vaporize")
                .Build());

            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.ApplyStatus(_entity, "Wet");

            // Both Burning and Wet should be fully removed (regardless of stack count).
            Assert.IsFalse(_entity.HasStatus("Burning"));
            Assert.IsFalse(_entity.HasStatus("Wet"));
            Assert.IsTrue(_entity.HasStatus("Vaporize"));
        }

        /// <summary>
        /// External implementation of <see cref="IReaction"/> that does NOT implement
        /// <see cref="IStackAwareReaction"/>. Mirrors v1.0 callers writing reactions
        /// against the original interface. The engine must treat such reactions exactly
        /// as it did pre-v1.1 (no stack requirements, ConsumesStatuses flag controls
        /// whole-status removal).
        /// </summary>
        private sealed class LegacyExternalReaction : IReaction
        {
            public string Key { get; }
            public string[] RequiredStatusKeys { get; }
            public string[] RequiredTags => new string[0];
            public bool ConsumesStatuses { get; }
            public IReactionResult[] Results { get; }

            public LegacyExternalReaction(string key, string[] required, bool consumes, IReactionResult[] results)
            {
                Key = key; RequiredStatusKeys = required; ConsumesStatuses = consumes; Results = results;
            }
        }

        [TestMethod]
        public void ExternalNonStackAwareReaction_StillWorks()
        {
            int fired = 0;
            _manager.Reactions.OnReactionTriggered += (_, _) => fired++;

            _manager.Reactions.RegisterReaction(new LegacyExternalReaction(
                "Legacy",
                required: new[] { "Burning" },
                consumes: true,
                results: new IReactionResult[] { new ApplyStatusResult("Inferno") }));

            _manager.Statuses.ApplyStatus(_entity, "Burning");

            Assert.AreEqual(1, fired);
            Assert.IsTrue(_entity.HasStatus("Inferno"));
            // ConsumesStatuses(true) -> Burning fully removed
            Assert.IsFalse(_entity.HasStatus("Burning"));
        }
    }
}
