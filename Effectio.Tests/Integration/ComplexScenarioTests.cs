using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Effects.Triggers;
using Effectio.Modifiers;
using Effectio.Reactions;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Integration
{
    /// <summary>
    /// Multi-system scenarios that exercise modifiers, effects, statuses, reactions, triggers,
    /// and custom actions in combination — the kind of thing a real game would wire up.
    /// </summary>
    [TestClass]
    public class ComplexScenarioTests
    {
        [TestMethod]
        public void ElementalCombat_FullLoop()
        {
            // Scenario: a player casts Fire on an enemy already Wet ? Vaporize triggers,
            // consuming both, dealing burst damage and applying Stunned. Meanwhile the
            // player has a passive Triggered effect that heals when HP drops below 30.
            var m = new EffectioManager();

            m.Statuses.RegisterStatus(StatusBuilder.Create("Burning").WithTags("Fire").WithDuration(5f)
                .OnTick(EffectBuilder.Create("burn_tick").Instant().AdjustStat("Health", -5f))
                .WithTickInterval(1f).Build());
            m.Statuses.RegisterStatus(StatusBuilder.Create("Wet").WithTags("Water").WithDuration(5f).Build());
            m.Statuses.RegisterStatus(new Status("Stunned", duration: 2f));

            m.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .AdjustStat("Health", -40f)
                .ApplyStatus("Stunned")
                .Build());

            var enemy = m.CreateEntity("enemy");
            enemy.AddStat(new Stat("Health", 100f, 0f, 100f));

            var player = m.CreateEntity("player");
            player.AddStat(new Stat("Health", 50f, 0f, 100f));
            m.Effects.ApplyEffect(player,
                EffectBuilder.Create("LastStand").Triggered(duration: 1000f)
                    .AdjustStat("Health", 20f).WhenStatBelow("Health", 30f).Build());

            // Step 1: enemy becomes Wet
            m.Statuses.ApplyStatus(enemy, "Wet");
            Assert.IsTrue(enemy.HasStatus("Wet"));

            // Step 2: fire applied ? Vaporize triggers (both consumed, -40 HP, Stunned)
            m.Statuses.ApplyStatus(enemy, "Burning");
            Assert.IsFalse(enemy.HasStatus("Burning"));
            Assert.IsFalse(enemy.HasStatus("Wet"));
            Assert.IsTrue(enemy.HasStatus("Stunned"));
            Assert.AreEqual(60f, enemy.GetStat("Health").CurrentValue);

            // Step 3: player gets hit below 30 HP ? triggered heal fires once
            player.GetStat("Health").BaseValue = 25f;
            player.GetStat("Health").Recalculate();
            m.Tick(0.1f);
            Assert.AreEqual(45f, player.GetStat("Health").CurrentValue);

            // Step 4: stun expires after 2s
            m.Tick(2.5f);
            Assert.IsFalse(enemy.HasStatus("Stunned"));
        }

        [TestMethod]
        public void StackedPolymorphicModifiers_ApplyInPriorityOrder()
        {
            // Base 100, +50 additive, +50 cap, *2 multiplicative, expected 300 (capped at 150 only if needed).
            var stat = new Stat("Damage", 100f, 0f, 120f);
            stat.AddModifier(new CapAdjustmentModifier("cap", 200f));       // max -> 320
            stat.AddModifier(new AdditiveModifier("flat", 50f));            // 150
            stat.AddModifier(new MultiplicativeModifier("dbl", 2f));        // 300, within cap

            Assert.AreEqual(300f, stat.CurrentValue);

            stat.RemoveModifier("cap");
            // Without the cap, value clamps to Max=120
            Assert.AreEqual(120f, stat.CurrentValue);
        }

        [TestMethod]
        public void AuraWithMultiplicativeModifier_UndoesOnExpiration()
        {
            var m = new EffectioManager();
            var p = m.CreateEntity("p");
            p.AddStat(new Stat("Damage", 10f, 0f, 1000f));

            var aura = EffectBuilder.Create("BerserkAura")
                .Aura(duration: 3f)
                .ApplyModifier("Damage", e => new MultiplicativeModifier(e.Key + "_mod", 1.5f, e.Duration, e.Key))
                .Build();

            m.Effects.ApplyEffect(p, aura);
            Assert.AreEqual(15f, p.GetStat("Damage").CurrentValue);

            m.Tick(4f); // expire

            Assert.AreEqual(10f, p.GetStat("Damage").CurrentValue);
            Assert.AreEqual(0, p.GetStat("Damage").Modifiers.Count);
        }

        [TestMethod]
        public void CompositeTrigger_AndOr_WorksTogether()
        {
            // Fire a heal when (HP < 30 AND (Has "Focused" OR Has "Rage"))
            var m = new EffectioManager();
            m.Statuses.RegisterStatus(new Status("Focused"));
            m.Statuses.RegisterStatus(new Status("Rage"));

            var p = m.CreateEntity("p");
            p.AddStat(new Stat("Health", 25f, 0f, 100f));

            var heal = EffectBuilder.Create("TacticalHeal")
                .Triggered(duration: 100f)
                .AdjustStat("Health", 15f)
                .When(new AndTrigger(
                    new StatBelowTrigger("Health", 30f),
                    new OrTrigger(new HasStatusTrigger("Focused"), new HasStatusTrigger("Rage"))))
                .Build();

            m.Effects.ApplyEffect(p, heal);
            m.Tick(0.1f);
            Assert.AreEqual(25f, p.GetStat("Health").CurrentValue, "no status yet — should not fire");

            m.Statuses.ApplyStatus(p, "Rage");
            m.Tick(0.1f);
            Assert.AreEqual(40f, p.GetStat("Health").CurrentValue);
        }

        [TestMethod]
        public void MultipleReactions_ChainCorrectly()
        {
            // Vaporize -> Overload chain: Burning+Wet makes Vaporized, Vaporized+Electrified makes Overloaded.
            var m = new EffectioManager();
            m.Statuses.RegisterStatus(new Status("Burning"));
            m.Statuses.RegisterStatus(new Status("Wet"));
            m.Statuses.RegisterStatus(new Status("Electrified"));
            m.Statuses.RegisterStatus(new Status("Vaporized"));
            m.Statuses.RegisterStatus(new Status("Overloaded"));

            m.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet").ConsumesStatuses().ApplyStatus("Vaporized").Build());

            m.Reactions.RegisterReaction(ReactionBuilder.Create("Overload")
                .RequireStatuses("Vaporized", "Electrified").ConsumesStatuses()
                .ApplyStatus("Overloaded").AdjustStat("Health", -50f).Build());

            var boss = m.CreateEntity("boss");
            boss.AddStat(new Stat("Health", 200f, 0f, 500f));

            m.Statuses.ApplyStatus(boss, "Electrified");
            m.Statuses.ApplyStatus(boss, "Burning");
            m.Statuses.ApplyStatus(boss, "Wet"); // triggers Vaporize ? which chains into Overload

            Assert.IsTrue(boss.HasStatus("Overloaded"));
            Assert.IsFalse(boss.HasStatus("Vaporized"));
            Assert.IsFalse(boss.HasStatus("Electrified"));
            Assert.AreEqual(150f, boss.GetStat("Health").CurrentValue);
        }

        [TestMethod]
        public void ManyEntitiesAndEffects_TickWithoutCrashingOrLeaking()
        {
            // Stress: 200 entities, each with 3 stats, one periodic DoT, one aura. Tick 100 times.
            var m = new EffectioManager();
            m.Statuses.RegisterStatus(StatusBuilder.Create("Bleed").WithDuration(1000f).WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("bleed_tick").Instant().AdjustStat("Health", -0.5f)).Build());

            var dot = EffectBuilder.Create("poison").Periodic(1000f, 0.5f).AdjustStat("Health", -0.25f).Build();
            var aura = EffectBuilder.Create("armor_aura").Aura(1000f).AdjustStat("Armor", 5f).Build();

            for (int i = 0; i < 200; i++)
            {
                var e = m.CreateEntity("e_" + i);
                e.AddStat(new Stat("Health", 1000f, 0f, 1000f));
                e.AddStat(new Stat("Damage", 10f));
                e.AddStat(new Stat("Armor", 0f, 0f, 1000f));

                m.Effects.ApplyEffect(e, dot);
                m.Effects.ApplyEffect(e, aura);
                m.Statuses.ApplyStatus(e, "Bleed");
            }

            for (int t = 0; t < 100; t++)
                m.Tick(1f / 60f);

            // Pick a sample — every entity should have lost some HP but still be alive.
            var sample = m.GetEntity("e_0");
            Assert.IsTrue(sample.GetStat("Health").CurrentValue < 1000f);
            Assert.IsTrue(sample.GetStat("Health").CurrentValue > 900f); // nothing catastrophic
            Assert.AreEqual(5f, sample.GetStat("Armor").CurrentValue);    // aura still applied
        }
    }
}
