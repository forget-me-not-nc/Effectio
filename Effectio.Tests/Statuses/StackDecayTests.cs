using System.Collections.Generic;
using Effectio.Builders;
using Effectio.Common;
using Effectio.Core;
using Effectio.Entities;
using Effectio.Statuses;
using Effectio.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Effectio.Tests.Statuses
{
    /// <summary>
    /// The two shapes a stacked status can lose its stacks in, and what a stack is worth
    /// while it is there.
    ///
    /// These are the tests that answer "should three stacks hurt three times as much, and
    /// should they fall off together or one at a time" - a design question rather than a
    /// technical one, so what matters here is that both answers are available and that
    /// neither is the accidental one.
    /// </summary>
    [TestClass]
    public class StackDecayTests
    {
        private static EffectioManager Victim(out IEffectioEntity entity, float health = 100f)
        {
            var manager = new EffectioManager();
            entity = manager.CreateEntity("victim");
            entity.AddStat(new Stat("Health", health, 0f, health));
            return manager;
        }

        private static IStatus Bleed(StackDecay decay, bool perStack)
        {
            var builder = StatusBuilder.Create("bleeding")
                .WithDuration(10f)
                .Stackable(5)
                .WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("bleed.tick").Instant().AdjustStat("Health", -1f).Build());

            if (decay == StackDecay.One) builder.DecayOneAtATime();
            if (perStack) builder.TicksPerStack();

            return builder.Build();
        }

        // ---------------------------------------------------------------- how they fall off

        [TestMethod]
        public void DecayAll_TakesEveryStackAtOnce()
        {
            var manager = Victim(out var entity);
            manager.Statuses.RegisterStatus(Bleed(StackDecay.All, perStack: false));

            for (var i = 0; i < 3; i++) manager.Statuses.ApplyStatus(entity, "bleeding");
            Assert.AreEqual(3, entity.GetStatusStackCount("bleeding"));

            manager.Tick(10f);

            Assert.IsFalse(manager.Statuses.HasStatus(entity, "bleeding"),
                "the v1.0 contract is that one timer ends the whole status");
            Assert.AreEqual(0, entity.GetStatusStackCount("bleeding"));
        }

        [TestMethod]
        public void DecayOne_DropsASingleStackAndWindsTheTimerBackUp()
        {
            var manager = Victim(out var entity);
            manager.Statuses.RegisterStatus(Bleed(StackDecay.One, perStack: false));
            var durations = (IRemainingDuration)manager.Statuses;

            for (var i = 0; i < 3; i++) manager.Statuses.ApplyStatus(entity, "bleeding");

            manager.Tick(10f);
            Assert.AreEqual(2, entity.GetStatusStackCount("bleeding"));
            Assert.AreEqual(10f, durations.GetRemainingDuration(entity, "bleeding"),
                "losing a stack should start the timer again");

            manager.Tick(10f);
            Assert.AreEqual(1, entity.GetStatusStackCount("bleeding"));

            manager.Tick(10f);
            Assert.IsFalse(manager.Statuses.HasStatus(entity, "bleeding"),
                "the last stack going should take the status with it");
        }

        [TestMethod]
        public void DecayOne_CostsFiveDurationsToLoseFiveStacks()
        {
            // The whole feel of this mode: climbing to five was five applications, and coming
            // back down is five durations rather than one.
            var manager = Victim(out var entity);
            manager.Statuses.RegisterStatus(Bleed(StackDecay.One, perStack: false));

            for (var i = 0; i < 5; i++) manager.Statuses.ApplyStatus(entity, "bleeding");

            // Stepped a second at a time, the way a game runs. One Tick call raises one
            // expiry event however large its delta, so a single Tick(40f) would drop one
            // stack rather than four.
            for (var second = 0; second < 40; second++) manager.Tick(1f);

            Assert.IsTrue(manager.Statuses.HasStatus(entity, "bleeding"),
                "four durations should not have finished a five stack pile");
            Assert.AreEqual(1, entity.GetStatusStackCount("bleeding"));

            for (var second = 0; second < 10; second++) manager.Tick(1f);
            Assert.IsFalse(manager.Statuses.HasStatus(entity, "bleeding"));
        }

        [TestMethod]
        public void DecayOne_AnnouncesEachStackLost()
        {
            var manager = Victim(out var entity);
            manager.Statuses.RegisterStatus(Bleed(StackDecay.One, perStack: false));

            var stackEvents = new List<string>();
            ((IStackOperations)manager.Statuses).OnStatusStacked += (e, key) => stackEvents.Add(key);

            for (var i = 0; i < 3; i++) manager.Statuses.ApplyStatus(entity, "bleeding");
            stackEvents.Clear();

            manager.Tick(10f);

            // A stack-gated reaction has to know the count fell, or a "three stacks of
            // bleeding" condition would stay armed after the third stack was gone.
            CollectionAssert.AreEqual(new[] { "bleeding" }, stackEvents);
        }

        [TestMethod]
        public void DecayIsAllByDefault()
        {
            Assert.AreEqual(StackDecay.All, StatusBuilder.Create("plain").Build().StackDecay,
                "existing content must not change meaning by upgrading");
        }

        // ---------------------------------------------------------------- what a stack is worth

        [TestMethod]
        public void WithoutPerStack_ThreeStacksTickLikeOne()
        {
            var manager = Victim(out var entity);
            manager.Statuses.RegisterStatus(Bleed(StackDecay.All, perStack: false));

            for (var i = 0; i < 3; i++) manager.Statuses.ApplyStatus(entity, "bleeding");
            manager.Tick(1f);

            Assert.AreEqual(99f, entity.GetStat("Health").CurrentValue,
                "the v1.0 contract: one tick per status, whatever the count");
        }

        [TestMethod]
        public void WithPerStack_ThreeStacksTickThreeTimes()
        {
            var manager = Victim(out var entity);
            manager.Statuses.RegisterStatus(Bleed(StackDecay.All, perStack: true));

            for (var i = 0; i < 3; i++) manager.Statuses.ApplyStatus(entity, "bleeding");
            manager.Tick(1f);

            Assert.AreEqual(97f, entity.GetStat("Health").CurrentValue,
                "three stacks that do not hurt three times as much are three of nothing");
        }

        [TestMethod]
        public void PerStackFollowsTheCountDown()
        {
            var manager = Victim(out var entity);
            manager.Statuses.RegisterStatus(Bleed(StackDecay.One, perStack: true));

            for (var i = 0; i < 3; i++) manager.Statuses.ApplyStatus(entity, "bleeding");

            // Nine seconds at three stacks.
            for (var second = 0; second < 9; second++) manager.Tick(1f);
            Assert.AreEqual(100f - 27f, entity.GetStat("Health").CurrentValue);

            // The tenth drops a stack - and still ticks, at the count it now has.
            manager.Tick(1f);
            Assert.AreEqual(2, entity.GetStatusStackCount("bleeding"));
            Assert.AreEqual(100f - 27f - 2f, entity.GetStat("Health").CurrentValue,
                "the second a stack falls off is still a second the status was on you");

            manager.Tick(1f);
            Assert.AreEqual(100f - 27f - 2f - 2f, entity.GetStat("Health").CurrentValue,
                "the damage should follow the count down as the pile drains");
        }
    }
}
