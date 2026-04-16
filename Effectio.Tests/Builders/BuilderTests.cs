using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Effects;
using Effectio.Reactions;
using Effectio.Statuses;

namespace Effectio.Tests.Builders
{
    [TestClass]
    public class BuilderTests
    {
        [TestMethod]
        public void EffectBuilder_BuildsInstantAdjustStat()
        {
            var effect = EffectBuilder.Create("heal").Instant().AdjustStat("Health", 25f).Build();

            Assert.AreEqual("heal", effect.Key);
            Assert.AreEqual(EffectType.Instant, effect.EffectType);
            Assert.AreEqual(EffectActionType.AdjustStat, effect.ActionType);
            Assert.AreEqual("Health", effect.TargetKey);
            Assert.AreEqual(25f, effect.Value);
        }

        [TestMethod]
        public void EffectBuilder_BuildsPeriodicWithDurationAndInterval()
        {
            var effect = EffectBuilder.Create("dot").Periodic(duration: 5f, tickInterval: 1f).AdjustStat("Health", -5f).Build();

            Assert.AreEqual(EffectType.Periodic, effect.EffectType);
            Assert.AreEqual(5f, effect.Duration);
            Assert.AreEqual(1f, effect.TickInterval);
            Assert.AreEqual(-5f, effect.Value);
        }

        [TestMethod]
        public void EffectBuilder_BuildsTriggeredWithCondition()
        {
            var effect = EffectBuilder.Create("lowhp")
                .Triggered(10f)
                .ApplyStatus("Berserk")
                .WhenStatBelow("Health", 30f)
                .Build();

            Assert.AreEqual(EffectType.Triggered, effect.EffectType);
            Assert.AreEqual(EffectActionType.ApplyStatus, effect.ActionType);
            Assert.AreEqual(TriggerConditionType.StatBelow, effect.TriggerCondition);
            Assert.AreEqual("Health", effect.TriggerKey);
            Assert.AreEqual(30f, effect.TriggerThreshold);
        }

        [TestMethod]
        public void StatusBuilder_BuildsWithTagsAndEffects()
        {
            var status = StatusBuilder.Create("Burning")
                .WithTags("Fire", "Elemental")
                .WithDuration(5f)
                .Stackable(3)
                .WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("burn_tick").Instant().AdjustStat("Health", -2f))
                .Build();

            Assert.AreEqual("Burning", status.Key);
            CollectionAssert.AreEqual(new[] { "Fire", "Elemental" }, status.Tags);
            Assert.AreEqual(5f, status.Duration);
            Assert.AreEqual(3, status.MaxStacks);
            Assert.AreEqual(1, status.OnTickEffects.Length);
            Assert.AreEqual("burn_tick", status.OnTickEffects[0].Key);
        }

        [TestMethod]
        public void ReactionBuilder_BuildsVaporizeReaction()
        {
            var reaction = ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .AdjustStat("Health", -50f)
                .ApplyStatus("Stunned")
                .Build();

            Assert.AreEqual("Vaporize", reaction.Key);
            CollectionAssert.AreEqual(new[] { "Burning", "Wet" }, reaction.RequiredStatusKeys);
            Assert.IsTrue(reaction.ConsumesStatuses);
            Assert.AreEqual(2, reaction.Results.Length);
            Assert.AreEqual(ReactionResultType.AdjustStat, reaction.Results[0].Type);
            Assert.AreEqual("Health", reaction.Results[0].TargetKey);
            Assert.AreEqual(-50f, reaction.Results[0].Value);
            Assert.AreEqual(ReactionResultType.ApplyStatus, reaction.Results[1].Type);
            Assert.AreEqual("Stunned", reaction.Results[1].TargetKey);
        }
    }
}
