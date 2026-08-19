using Effectio.Builders;
using Effectio.Core;
using Effectio.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Effectio.Tests.Stats
{
    /// <summary>
    /// Locks down the v1.1 fix: <see cref="IStat.BaseValue"/> never leaves the stat's own
    /// bounds.
    ///
    /// Before v1.1 only <see cref="IStat.CurrentValue"/> was clamped, so a base driven past a
    /// limit kept going. Nothing looked wrong at the time - the current value was correct -
    /// and the damage only surfaced later, when a heal had to pay off an invisible debt before
    /// anything moved. These tests exist because that failure is silent at the moment it is
    /// caused.
    /// </summary>
    [TestClass]
    public class BaseValueBoundsTests
    {
        [TestMethod]
        public void SustainedDamage_DoesNotDriveBaseBelowMin()
        {
            var health = new Stat("Health", 100f, 0f, 100f);

            // Twice as many ticks as it takes to reach the floor.
            for (var i = 0; i < 40; i++)
            {
                health.BaseValue -= 5f;
                health.Recalculate();
            }

            Assert.AreEqual(0f, health.BaseValue, "base was allowed past its own floor");
            Assert.AreEqual(0f, health.CurrentValue);
        }

        [TestMethod]
        public void HealingAfterSustainedDamage_IsFeltImmediately()
        {
            var health = new Stat("Health", 100f, 0f, 100f);

            for (var i = 0; i < 40; i++)
            {
                health.BaseValue -= 5f;
                health.Recalculate();
            }

            health.BaseValue += 50f;
            health.Recalculate();

            // The bug this test is named after: 50 of healing arrived and nothing moved,
            // because the base had a hundred points of debt to clear first.
            Assert.AreEqual(50f, health.CurrentValue);
        }

        [TestMethod]
        public void SustainedOverheal_DoesNotDriveBaseAboveMax()
        {
            var health = new Stat("Health", 100f, 0f, 100f);

            for (var i = 0; i < 20; i++)
            {
                health.BaseValue += 5f;
                health.Recalculate();
            }

            Assert.AreEqual(100f, health.BaseValue);

            health.BaseValue -= 30f;
            health.Recalculate();

            // The mirror of the healing case: damage that lands on invisible surplus.
            Assert.AreEqual(70f, health.CurrentValue);
        }

        [TestMethod]
        public void Constructor_ClampsABaseOutsideItsBounds()
        {
            Assert.AreEqual(10f, new Stat("Clamped", 999f, 0f, 10f).BaseValue);
            Assert.AreEqual(0f, new Stat("Clamped", -999f, 0f, 10f).BaseValue);
        }

        [TestMethod]
        public void MovingTheBounds_PullsTheBaseInside()
        {
            var stat = new Stat("Strength", 50f, 0f, 100f);

            stat.Max = 30f;
            Assert.AreEqual(30f, stat.BaseValue, "lowering the ceiling left the base above it");

            stat.Min = 40f;
            Assert.AreEqual(40f, stat.BaseValue, "raising the floor left the base below it");
        }

        [TestMethod]
        public void ModifiersStillReachBeyondTheBase()
        {
            // The fix must not stop a modifier lifting the current value past the base's own
            // ceiling - raising a limit is exactly what a cap modifier is for.
            var health = new Stat("Health", 100f, 0f, 100f);

            health.AddModifier(ModifierBuilder.Create("blessing")
                .CapAdjustment(50f)
                .Permanent()
                .Build());

            health.AddModifier(ModifierBuilder.Create("vigour")
                .Additive(40f)
                .Permanent()
                .Build());

            health.Recalculate();

            Assert.AreEqual(140f, health.CurrentValue, "a cap modifier could no longer raise the ceiling");
            Assert.AreEqual(100f, health.BaseValue, "the modifier leaked into the base");
        }

        [TestMethod]
        public void PeriodicDamage_ThroughTheEngine_LeavesHealingWorking()
        {
            // The same failure by the route a game actually takes: a damage-over-time effect
            // applied through the manager rather than a stat poked by hand.
            var manager = new EffectioManager();
            var entity = manager.CreateEntity("victim");
            entity.AddStat(new Stat("Health", 100f, 0f, 100f));

            var burning = EffectBuilder.Create("burning")
                .Periodic(duration: 30f, tickInterval: 1f)
                .AdjustStat("Health", -10f)
                .Build();

            manager.Effects.ApplyEffect(entity, burning);

            for (var i = 0; i < 30; i++)
            {
                manager.Tick(1f);
            }

            Assert.AreEqual(0f, entity.GetStat("Health").CurrentValue);

            var heal = EffectBuilder.Create("mend")
                .Instant()
                .AdjustStat("Health", 25f)
                .Build();

            manager.Effects.ApplyEffect(entity, heal);

            Assert.AreEqual(25f, entity.GetStat("Health").CurrentValue,
                "healing after a long burn did nothing, which is the bug this release fixes");
        }
    }
}
