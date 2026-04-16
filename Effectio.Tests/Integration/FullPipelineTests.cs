using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Reactions;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Integration
{
    [TestClass]
    public class FullPipelineTests
    {
        [TestMethod]
        public void FullPipeline_FireWaterVaporize()
        {
            // Setup manager
            var manager = new EffectioManager();

            // Register statuses
            manager.Statuses.RegisterStatus(new Status("Burning", tags: new[] { "Fire" }, duration: 10f));
            manager.Statuses.RegisterStatus(new Status("Wet", tags: new[] { "Water" }, duration: 10f));
            manager.Statuses.RegisterStatus(new Status("Vaporized", duration: 5f));

            // Register reaction: Burning + Wet = Vaporize
            manager.Reactions.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized"),
                    new ReactionResult(ReactionResultType.AdjustStat, "Health", -50f)
                }
            ));

            // Create entity with stats
            var entity = manager.CreateEntity("player1");
            entity.AddStat(new Stat("Health", 200f, 0f, 500f));

            // Apply fire effect → applies Burning status
            var fireEffect = new Effect("fire_blast", EffectType.Instant, EffectActionType.ApplyStatus, "Burning");
            manager.Effects.ApplyEffect(entity, fireEffect);

            Assert.IsTrue(entity.HasStatus("Burning"));
            Assert.AreEqual(200f, entity.GetStat("Health").CurrentValue);

            // Apply water effect → applies Wet status → triggers Vaporize reaction
            var waterEffect = new Effect("water_splash", EffectType.Instant, EffectActionType.ApplyStatus, "Wet");
            manager.Effects.ApplyEffect(entity, waterEffect);

            // Vaporize should have triggered:
            // - Burning and Wet consumed
            // - Vaporized applied
            // - Health reduced by 50
            Assert.IsFalse(entity.HasStatus("Burning"));
            Assert.IsFalse(entity.HasStatus("Wet"));
            Assert.IsTrue(entity.HasStatus("Vaporized"));
            Assert.AreEqual(150f, entity.GetStat("Health").CurrentValue);
        }

        [TestMethod]
        public void FullPipeline_SequentialApplication_FirstReactionWins()
        {
            // When statuses are applied sequentially, the first matching reaction triggers immediately.
            // Burning applied, then Wet → Vaporize triggers (consuming both).
            // Electrified is applied after Wet is already consumed, so ElectroShock does NOT trigger.
            var manager = new EffectioManager();

            manager.Statuses.RegisterStatus(new Status("Burning", tags: new[] { "Fire" }));
            manager.Statuses.RegisterStatus(new Status("Wet", tags: new[] { "Water" }));
            manager.Statuses.RegisterStatus(new Status("Electrified", tags: new[] { "Electric" }));
            manager.Statuses.RegisterStatus(new Status("Vaporized"));
            manager.Statuses.RegisterStatus(new Status("Shocked"));

            manager.Reactions.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            manager.Reactions.RegisterReaction(new Reaction(
                "ElectroShock",
                requiredStatusKeys: new[] { "Wet", "Electrified" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Shocked")
                }
            ));

            var entity = manager.CreateEntity("enemy1");

            // Sequential: Fire → Water (triggers Vaporize) → Electric (no reaction — Water already consumed)
            manager.Effects.ApplyEffect(entity, new Effect("fire", EffectType.Instant, EffectActionType.ApplyStatus, "Burning"));
            manager.Effects.ApplyEffect(entity, new Effect("water", EffectType.Instant, EffectActionType.ApplyStatus, "Wet"));
            manager.Effects.ApplyEffect(entity, new Effect("electric", EffectType.Instant, EffectActionType.ApplyStatus, "Electrified"));

            Assert.IsTrue(entity.HasStatus("Vaporized"));
            Assert.IsFalse(entity.HasStatus("Shocked")); // Wet was already consumed
            Assert.IsTrue(entity.HasStatus("Electrified")); // Still active, no matching reaction
        }

        [TestMethod]
        public void FullPipeline_AllStatusesPresent_BothReactionsFire()
        {
            // When all three statuses coexist BEFORE reaction check,
            // both Vaporize and ElectroShock trigger simultaneously.
            var manager = new EffectioManager();

            manager.Statuses.RegisterStatus(new Status("Burning", tags: new[] { "Fire" }));
            manager.Statuses.RegisterStatus(new Status("Wet", tags: new[] { "Water" }));
            manager.Statuses.RegisterStatus(new Status("Electrified", tags: new[] { "Electric" }));
            manager.Statuses.RegisterStatus(new Status("Vaporized"));
            manager.Statuses.RegisterStatus(new Status("Shocked"));

            manager.Reactions.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            manager.Reactions.RegisterReaction(new Reaction(
                "ElectroShock",
                requiredStatusKeys: new[] { "Wet", "Electrified" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Shocked")
                }
            ));

            var entity = manager.CreateEntity("enemy1");

            // Pre-apply all statuses directly to entity (bypassing auto-reaction-check)
            entity.AddStatus("Burning");
            entity.AddStatus("Wet");
            entity.AddStatus("Electrified");

            // Manually trigger reaction check — both reactions fire simultaneously
            manager.Reactions.CheckReactions(entity);

            Assert.IsTrue(entity.HasStatus("Vaporized"));
            Assert.IsTrue(entity.HasStatus("Shocked"));
            Assert.IsFalse(entity.HasStatus("Burning"));
            Assert.IsFalse(entity.HasStatus("Wet"));
            Assert.IsFalse(entity.HasStatus("Electrified"));
        }

        [TestMethod]
        public void FullPipeline_ReactionChaining()
        {
            var manager = new EffectioManager();

            manager.Statuses.RegisterStatus(new Status("Burning"));
            manager.Statuses.RegisterStatus(new Status("Wet"));
            manager.Statuses.RegisterStatus(new Status("Electrified"));
            manager.Statuses.RegisterStatus(new Status("Vaporized"));
            manager.Statuses.RegisterStatus(new Status("Overloaded"));

            // Chain: Burning+Wet→Vaporized, then Vaporized+Electrified→Overloaded
            manager.Reactions.RegisterReaction(new Reaction(
                "Vaporize",
                requiredStatusKeys: new[] { "Burning", "Wet" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Vaporized")
                }
            ));

            manager.Reactions.RegisterReaction(new Reaction(
                "Overload",
                requiredStatusKeys: new[] { "Vaporized", "Electrified" },
                consumesStatuses: true,
                results: new IReactionResult[]
                {
                    new ReactionResult(ReactionResultType.ApplyStatus, "Overloaded"),
                    new ReactionResult(ReactionResultType.AdjustStat, "Health", -100f)
                }
            ));

            var entity = manager.CreateEntity("boss");
            entity.AddStat(new Stat("Health", 500f, 0f, 1000f));

            // Pre-apply Electrified
            manager.Effects.ApplyEffect(entity, new Effect("shock", EffectType.Instant, EffectActionType.ApplyStatus, "Electrified"));

            // Now apply Burning, then Wet — Vaporize triggers, then chains into Overload
            manager.Effects.ApplyEffect(entity, new Effect("fire", EffectType.Instant, EffectActionType.ApplyStatus, "Burning"));
            manager.Effects.ApplyEffect(entity, new Effect("water", EffectType.Instant, EffectActionType.ApplyStatus, "Wet"));

            Assert.IsTrue(entity.HasStatus("Overloaded"));
            Assert.IsFalse(entity.HasStatus("Vaporized"));   // consumed by Overload chain
            Assert.IsFalse(entity.HasStatus("Electrified")); // consumed by Overload
            Assert.AreEqual(400f, entity.GetStat("Health").CurrentValue); // 500 - 100
        }

        [TestMethod]
        public void FullPipeline_PeriodicDamageOverTime()
        {
            var manager = new EffectioManager();

            // Burning status that deals 10 damage per tick
            manager.Statuses.RegisterStatus(new Status(
                "Burning",
                tags: new[] { "Fire" },
                duration: 3f,
                onTickEffects: new IEffect[]
                {
                    new Effect("burn_tick", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: -10f)
                },
                tickInterval: 1f
            ));

            var entity = manager.CreateEntity("target");
            entity.AddStat(new Stat("Health", 100f, 0f, 500f));

            // Apply burning
            manager.Statuses.ApplyStatus(entity, "Burning");
            Assert.IsTrue(entity.HasStatus("Burning"));

            // Tick 1 second — should deal 10 damage
            manager.Tick(1f);
            Assert.AreEqual(90f, entity.GetStat("Health").CurrentValue);

            // Tick another second — another 10
            manager.Tick(1f);
            Assert.AreEqual(80f, entity.GetStat("Health").CurrentValue);
        }

        [TestMethod]
        public void FullPipeline_EntityManagement()
        {
            var manager = new EffectioManager();

            var entity = manager.CreateEntity("npc1");
            entity.AddStat(new Stat("Health", 50f, 0f, 100f));

            var retrieved = manager.GetEntity("npc1");
            Assert.AreEqual("npc1", retrieved.Id);
            Assert.AreEqual(50f, retrieved.GetStat("Health").CurrentValue);

            manager.RemoveEntity("npc1");

            // GetEntity throws for missing entities
            Assert.ThrowsException<System.Collections.Generic.KeyNotFoundException>(() => manager.GetEntity("npc1"));

            // TryGetEntity returns false instead of throwing
            Assert.IsFalse(manager.TryGetEntity("npc1", out _));
        }

        [TestMethod]
        public void FullPipeline_TryGetPatterns()
        {
            var manager = new EffectioManager();
            var entity = manager.CreateEntity("test");
            entity.AddStat(new Stat("Health", 100f, 0f, 500f));

            // TryGetEntity succeeds
            Assert.IsTrue(manager.TryGetEntity("test", out var found));
            Assert.AreEqual("test", found.Id);

            // TryGetStat succeeds
            Assert.IsTrue(entity.TryGetStat("Health", out var health));
            Assert.AreEqual(100f, health.CurrentValue);

            // TryGetStat fails gracefully
            Assert.IsFalse(entity.TryGetStat("NonExistent", out var missing));
            Assert.IsNull(missing);
        }
    }
}
