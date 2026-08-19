using System;
using System.Collections.Generic;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects.Actions;
using Effectio.Entities;
using Effectio.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Effectio.Tests.Core
{
    /// <summary>
    /// What happens when the world changes shape while it is being ticked.
    ///
    /// Everything a tick runs is somebody else's code - effect actions, status ticks,
    /// reaction results - and the most ordinary things that code does are to kill something
    /// or make something. A simulation that cannot survive being asked to do either during
    /// its own update is one every consumer eventually crashes.
    /// </summary>
    [TestClass]
    public class TickReentrancyTests
    {
        private sealed class CallbackAction : IEffectAction
        {
            private readonly Action<EffectActionContext> _run;
            public CallbackAction(Action<EffectActionContext> run) => _run = run;
            public void Execute(in EffectActionContext ctx) => _run(ctx);
            public void Undo(in EffectActionContext ctx) { }
        }

        private static IEffectioEntity WithHealth(EffectioManager manager, string id)
        {
            var entity = manager.CreateEntity(id);
            entity.AddStat(new Stat("Health", 10f, 0f, 10f));
            return entity;
        }

        private static Effectio.Effects.IEffect EverySecond(string key, Action<EffectActionContext> run)
        {
            return EffectBuilder.Create(key)
                .Periodic(duration: 60f, tickInterval: 1f)
                .WithAction(new CallbackAction(run))
                .Build();
        }

        [TestMethod]
        public void SpawningAnEntityFromInsideATickDoesNotThrow()
        {
            // The summon, the slime that splits, the corpse that gets back up. Before this
            // was fixed the dictionary was being enumerated and the add threw
            // InvalidOperationException - a hard crash from content anyone would write.
            var manager = new EffectioManager();
            var summoner = WithHealth(manager, "summoner");

            var spawned = 0;
            manager.Effects.ApplyEffect(summoner, EverySecond("summon", _ =>
            {
                WithHealth(manager, "minion" + spawned);
                spawned++;
            }));

            manager.Tick(1f);
            manager.Tick(1f);

            Assert.AreEqual(2, spawned);
            Assert.IsTrue(manager.TryGetEntity("minion0", out _));
            Assert.IsTrue(manager.TryGetEntity("minion1", out _));
        }

        [TestMethod]
        public void ASpawnedEntityWaitsForTheNextTick()
        {
            // Snapshot semantics, and the honest ones: a thing made half way through a frame
            // did not exist for the first half of it.
            var manager = new EffectioManager();
            var summoner = WithHealth(manager, "summoner");

            var minionTicks = 0;
            var minionEffect = EverySecond("minion.tick", _ => minionTicks++);

            var spawnedOnce = false;
            manager.Effects.ApplyEffect(summoner, EverySecond("summon", _ =>
            {
                if (spawnedOnce) return;
                spawnedOnce = true;
                manager.Effects.ApplyEffect(WithHealth(manager, "minion"), minionEffect);
            }));

            manager.Tick(1f);
            Assert.AreEqual(0, minionTicks, "the minion should not act in the frame it appeared in");

            manager.Tick(1f);
            Assert.AreEqual(1, minionTicks);
        }

        [TestMethod]
        public void RemovingAnEntityFromInsideATickDoesNotThrow()
        {
            var manager = new EffectioManager();
            var doomed = WithHealth(manager, "doomed");
            WithHealth(manager, "bystander");

            manager.Effects.ApplyEffect(doomed, EverySecond("reaper", ctx => manager.RemoveEntity(ctx.Entity.Id)));

            manager.Tick(1f);

            Assert.IsFalse(manager.TryGetEntity("doomed", out _));
            Assert.IsTrue(manager.TryGetEntity("bystander", out _));
        }

        [TestMethod]
        public void AnEntityRemovedEarlyInATickIsNotTickedLaterInIt()
        {
            // Two entities, and the first one's effect kills the second. The second must not
            // go on to take its own turn in the same frame.
            var manager = new EffectioManager();
            var killer = WithHealth(manager, "a.killer");
            var victim = WithHealth(manager, "b.victim");

            var victimTicks = 0;
            manager.Effects.ApplyEffect(victim, EverySecond("victim.tick", _ => victimTicks++));
            manager.Effects.ApplyEffect(killer, EverySecond("kill", _ => manager.RemoveEntity("b.victim")));

            manager.Tick(1f);

            Assert.IsFalse(manager.TryGetEntity("b.victim", out _));
            Assert.AreEqual(0, victimTicks, "a dead entity kept acting for the rest of the frame");
        }

        [TestMethod]
        public void AnIdReusedInsideOneTickDoesNotTickTheGhost()
        {
            // Pathological but cheap to be right about: something dies and something else
            // registers under its name before the loop reaches it.
            var manager = new EffectioManager();
            var first = WithHealth(manager, "a.trigger");
            var ghost = WithHealth(manager, "b.reused");

            var ghostTicks = 0;
            manager.Effects.ApplyEffect(ghost, EverySecond("ghost.tick", _ => ghostTicks++));

            manager.Effects.ApplyEffect(first, EverySecond("replace", _ =>
            {
                manager.RemoveEntity("b.reused");
                WithHealth(manager, "b.reused");
            }));

            manager.Tick(1f);

            Assert.AreEqual(0, ghostTicks, "the replaced entity was ticked after it had been replaced");
        }

        [TestMethod]
        public void ApplyingAStatusFromInsideAStatusTickDoesNotThrow()
        {
            var manager = new EffectioManager();
            var entity = WithHealth(manager, "spreader");

            manager.Statuses.RegisterStatus(StatusBuilder.Create("spreading")
                .WithDuration(10f).WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("spread").Instant()
                    .WithAction(new CallbackAction(ctx => manager.Statuses.ApplyStatus(ctx.Entity, "second")))
                    .Build())
                .Build());

            manager.Statuses.RegisterStatus(StatusBuilder.Create("second").WithDuration(5f).Build());

            manager.Statuses.ApplyStatus(entity, "spreading");
            manager.Tick(1f);

            Assert.IsTrue(manager.Statuses.HasStatus(entity, "second"));
        }

        [TestMethod]
        public void AddingAStatFromInsideAStatChangeDoesNotThrow()
        {
            // The same hazard one level down. A modifier expiring recalculates its stat,
            // which raises OnValueChanged - somebody else's code, free to give the entity a
            // stat it did not have while the tick loop is walking them.
            var manager = new EffectioManager();
            var entity = WithHealth(manager, "changer");

            var health = entity.GetStat("Health");

            health.AddModifier(ModifierBuilder.Create("brief")
                .Additive(-5f)
                .WithDuration(1f)
                .Build());

            health.Recalculate();

            // Subscribed only now, so the one change this handler sees is the one the tick
            // causes when the modifier expires. Hooked up earlier it would fire on the line
            // above instead, outside any loop, and prove nothing.
            health.OnValueChanged += (_, _, _) => entity.AddStat(new Stat("Fury", 0f, 0f, 100f));

            manager.Tick(1.5f);

            Assert.IsTrue(entity.HasStat("Fury"));
        }

        [TestMethod]
        public void ManySpawnsInOneTick()
        {
            // A crowd arriving at once, which is what an area effect looks like.
            var manager = new EffectioManager();
            var caster = WithHealth(manager, "caster");
            var made = new List<string>();

            manager.Effects.ApplyEffect(caster, EverySecond("swarm", _ =>
            {
                for (var i = 0; i < 50; i++)
                {
                    var id = "swarm" + made.Count;
                    WithHealth(manager, id);
                    made.Add(id);
                }
            }));

            manager.Tick(1f);

            Assert.AreEqual(50, made.Count);
            Assert.IsTrue(manager.TryGetEntity("swarm49", out _));
        }
    }
}
