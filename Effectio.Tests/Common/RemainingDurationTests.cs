using Effectio.Builders;
using Effectio.Common;
using Effectio.Core;
using Effectio.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Effectio.Tests.Common
{
    /// <summary>
    /// Pins down <see cref="IRemainingDuration"/>: the number an interface needs to draw a
    /// timer, and the three answers it can give.
    /// </summary>
    [TestClass]
    public class RemainingDurationTests
    {
        private static EffectioManager ManagerWithVictim(out Effectio.Entities.IEffectioEntity entity)
        {
            var manager = new EffectioManager();
            entity = manager.CreateEntity("victim");
            entity.AddStat(new Stat("Health", 100f, 0f, 100f));
            return manager;
        }

        [TestMethod]
        public void Status_CountsDownAndReportsZeroOnceGone()
        {
            var manager = ManagerWithVictim(out var entity);
            var durations = (IRemainingDuration)manager.Statuses;

            manager.Statuses.RegisterStatus(StatusBuilder.Create("burning").WithDuration(10f).Build());

            Assert.AreEqual(0f, durations.GetRemainingDuration(entity, "burning"),
                "a status nobody has should read as absent");

            manager.Statuses.ApplyStatus(entity, "burning");
            Assert.AreEqual(10f, durations.GetRemainingDuration(entity, "burning"));

            manager.Tick(4f);
            Assert.AreEqual(6f, durations.GetRemainingDuration(entity, "burning"), 0.0001f);

            manager.Tick(6f);
            Assert.AreEqual(0f, durations.GetRemainingDuration(entity, "burning"),
                "an expired status should be absent, not zero-with-a-body");
        }

        [TestMethod]
        public void Status_RefreshingPutsTheFullDurationBack()
        {
            var manager = ManagerWithVictim(out var entity);
            var durations = (IRemainingDuration)manager.Statuses;

            manager.Statuses.RegisterStatus(
                StatusBuilder.Create("burning").WithDuration(10f).Stackable(3).Build());

            manager.Statuses.ApplyStatus(entity, "burning");
            manager.Tick(7f);
            Assert.AreEqual(3f, durations.GetRemainingDuration(entity, "burning"), 0.0001f);

            // The reason a shadow clock outside the engine cannot work: this is ordinary use,
            // and it moves the number without any event that carries the new value.
            manager.Statuses.ApplyStatus(entity, "burning");
            Assert.AreEqual(10f, durations.GetRemainingDuration(entity, "burning"));
        }

        [TestMethod]
        public void Status_PermanentReportsMinusOne()
        {
            var manager = ManagerWithVictim(out var entity);
            var durations = (IRemainingDuration)manager.Statuses;

            // Permanent is what a status with no lifetime means.
            manager.Statuses.RegisterStatus(StatusBuilder.Create("cursed").Permanent().Build());
            manager.Statuses.ApplyStatus(entity, "cursed");

            Assert.AreEqual(-1f, durations.GetRemainingDuration(entity, "cursed"));

            manager.Tick(100f);
            Assert.AreEqual(-1f, durations.GetRemainingDuration(entity, "cursed"),
                "a permanent status should not start counting down");
        }

        [TestMethod]
        public void Effect_CountsDownAndReportsZeroOnceGone()
        {
            var manager = ManagerWithVictim(out var entity);
            var durations = (IRemainingDuration)manager.Effects;

            var shield = EffectBuilder.Create("shield")
                .Timed(8f)
                .ApplyModifier("Health", 20f)
                .Build();

            Assert.AreEqual(0f, durations.GetRemainingDuration(entity, "shield"));

            manager.Effects.ApplyEffect(entity, shield);
            Assert.AreEqual(8f, durations.GetRemainingDuration(entity, "shield"));

            manager.Tick(3f);
            Assert.AreEqual(5f, durations.GetRemainingDuration(entity, "shield"), 0.0001f);

            manager.Tick(5f);
            Assert.AreEqual(0f, durations.GetRemainingDuration(entity, "shield"));
        }

        [TestMethod]
        public void Effect_TwoOfTheSameKeyReportTheLongest()
        {
            var manager = ManagerWithVictim(out var entity);
            var durations = (IRemainingDuration)manager.Effects;

            var shield = EffectBuilder.Create("shield")
                .Timed(8f)
                .ApplyModifier("Health", 20f)
                .Build();

            manager.Effects.ApplyEffect(entity, shield);
            manager.Tick(5f);
            manager.Effects.ApplyEffect(entity, shield);

            // One copy has 3 left and the other has 8. A timer drawn for "shield" should end
            // when being shielded ends, not when the first copy happens to run out.
            Assert.AreEqual(8f, durations.GetRemainingDuration(entity, "shield"), 0.0001f);
        }

        [TestMethod]
        public void BothEnginesAdvertiseTheInterface()
        {
            var manager = new EffectioManager();

            // A consumer holding only the v1.0 interfaces asks by type check rather than by
            // version number, which is the whole point of keeping this separate.
            Assert.IsInstanceOfType(manager.Statuses, typeof(IRemainingDuration));
            Assert.IsInstanceOfType(manager.Effects, typeof(IRemainingDuration));
        }
    }
}
