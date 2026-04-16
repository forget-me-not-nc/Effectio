using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Entities;
using Effectio.Reactions;
using Effectio.Statuses;

namespace Effectio.Tests.Reactions
{
    [TestClass]
    public class ReactionEngineTests
    {
        private StatusEngine _statusEngine;
        private ReactionEngine _reactionEngine;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _statusEngine = new StatusEngine();
            _reactionEngine = new ReactionEngine(_statusEngine);

            _entity = new EffectioEntity("player1");

            _statusEngine.RegisterStatus(new Status("Burning", tags: new[] { "Fire", "DoT" }));
            _statusEngine.RegisterStatus(new Status("Wet", tags: new[] { "Water" }));
            _statusEngine.RegisterStatus(new Status("Electrified", tags: new[] { "Electric" }));
            _statusEngine.RegisterStatus(new Status("Vaporized"));
            _statusEngine.RegisterStatus(new Status("Shocked"));
        }

        [TestMethod]
        public void SimpleReaction_TriggersWhenBothStatusesPresent()
        {
            _reactionEngine.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            // Apply both statuses
            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");

            // Check reactions
            _reactionEngine.CheckReactions(_entity);

            // Burning and Wet should be consumed, Vaporized should be applied
            Assert.IsFalse(_entity.HasStatus("Burning"));
            Assert.IsFalse(_entity.HasStatus("Wet"));
            Assert.IsTrue(_entity.HasStatus("Vaporized"));
        }

        [TestMethod]
        public void Reaction_DoesNotTriggerIfMissingStatus()
        {
            _reactionEngine.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            _statusEngine.ApplyStatus(_entity, "Burning");
            // No "Wet" status
            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Burning"));
            Assert.IsFalse(_entity.HasStatus("Vaporized"));
        }

        [TestMethod]
        public void MultipleReactions_FireSimultaneously()
        {
            _reactionEngine.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            _reactionEngine.RegisterReaction(new Reaction(
                "ElectroShock",
                requiredStatusKeys: new[] { "Wet", "Electrified" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Shocked")
                }
            ));

            // Apply all three
            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _statusEngine.ApplyStatus(_entity, "Electrified");

            _reactionEngine.CheckReactions(_entity);

            // Both reactions should have triggered
            Assert.IsTrue(_entity.HasStatus("Vaporized"));
            Assert.IsTrue(_entity.HasStatus("Shocked"));
            // All consumed statuses removed
            Assert.IsFalse(_entity.HasStatus("Burning"));
            Assert.IsFalse(_entity.HasStatus("Wet"));
            Assert.IsFalse(_entity.HasStatus("Electrified"));
        }

        [TestMethod]
        public void TagBasedReaction_MatchesByTags()
        {
            _reactionEngine.RegisterReaction(new Reaction(
                "ElementalClash",
                requiredTags: new[] { "Fire", "Water" },
                consumesStatuses: false,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            _statusEngine.ApplyStatus(_entity, "Burning");  // has Fire tag
            _statusEngine.ApplyStatus(_entity, "Wet");       // has Water tag

            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Vaporized"));
        }

        [TestMethod]
        public void ReactionChaining_NewStatusTriggersNextReaction()
        {
            _statusEngine.RegisterStatus(new Status("Overloaded"));

            _reactionEngine.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            _reactionEngine.RegisterReaction(new Reaction(
                "Overload",
                requiredStatusKeys: new[] { "Vaporized", "Electrified" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Overloaded")
                }
            ));

            // Apply statuses that will chain: Burning+Wet→Vaporized, then Vaporized+Electrified→Overloaded
            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _statusEngine.ApplyStatus(_entity, "Electrified");

            _reactionEngine.CheckReactions(_entity);

            Assert.IsTrue(_entity.HasStatus("Overloaded"));
            Assert.IsFalse(_entity.HasStatus("Vaporized")); // consumed by Overload
            Assert.IsFalse(_entity.HasStatus("Electrified")); // consumed by Overload
        }

        [TestMethod]
        public void ReactionTriggeredEvent_Fires()
        {
            string triggeredKey = null;
            _reactionEngine.OnReactionTriggered += (e, r) => triggeredKey = r.Key;

            _reactionEngine.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[] { new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized") }
            ));

            _statusEngine.ApplyStatus(_entity, "Burning");
            _statusEngine.ApplyStatus(_entity, "Wet");
            _reactionEngine.CheckReactions(_entity);

            Assert.AreEqual("Vaporize", triggeredKey);
        }

        [TestMethod]
        public void ChainDepthLimit_PreventsInfiniteLoops()
        {
            // Create a cycle: A+B→C, C→A (this would loop without depth limit)
            _statusEngine.RegisterStatus(new Status("A"));
            _statusEngine.RegisterStatus(new Status("B"));
            _statusEngine.RegisterStatus(new Status("C"));

            _reactionEngine.MaxChainDepth = 3;

            _reactionEngine.RegisterReaction(new Reaction(
                "AB_to_C",
                requiredStatusKeys: new[] { "A", "B" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "C"),
                    new ReactionResult(ReactionResultType.ApplyStatus, "A")
                }
            ));

            _reactionEngine.RegisterReaction(new Reaction(
                "CA_to_B",
                requiredStatusKeys: new[] { "C", "A" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "A"),
                    new ReactionResult(ReactionResultType.ApplyStatus, "B")
                }
            ));

            _statusEngine.ApplyStatus(_entity, "A");
            _statusEngine.ApplyStatus(_entity, "B");

            // Should not hang — depth limit stops it
            _reactionEngine.CheckReactions(_entity);

            // Just verify it completes without infinite loop
            Assert.IsTrue(true);
        }
    }
}
