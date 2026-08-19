using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Effects.Actions;
using Effectio.Entities;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Statuses
{
    /// <summary>
    /// End-to-end coverage for the v1.1 <c>OnRefresh</c> status hook:
    /// <see cref="StatusBuilder.OnRefresh(IEffect)"/> +
    /// <see cref="IStatusEngine.OnStatusRefreshed"/> +
    /// <see cref="IStatus.OnRefreshEffects"/>. Verifies the firing rules
    /// across all <c>ApplyStatus</c> paths and the regression that
    /// statuses without OnRefresh effects keep their v1.0 behaviour.
    /// </summary>
    [TestClass]
    public class StatusOnRefreshTests
    {
        private EffectioManager _manager;
        private IEffectioEntity _entity;
        private List<string> _eventLog;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
            _entity = _manager.CreateEntity("p");
            _entity.AddStat(new Stat("Health", 100f, 0f, 1000f));
            _eventLog = new List<string>();
            _manager.Statuses.OnStatusApplied += (_, k) => _eventLog.Add($"applied:{k}");
            _manager.Statuses.OnStatusRefreshed += (_, k) => _eventLog.Add($"refreshed:{k}");
        }

        // -------- OnStatusRefreshed event firing rules --------

        [TestMethod]
        public void OnStatusRefreshed_DoesNotFireOnFirstApplication()
        {
            _manager.Statuses.RegisterStatus(new Status("Burning", duration: 5f, maxStacks: 3));

            _manager.Statuses.ApplyStatus(_entity, "Burning");

            CollectionAssert.AreEqual(new[] { "applied:Burning" }, _eventLog,
                "First application fires OnStatusApplied only; OnStatusRefreshed must NOT fire on birth.");
        }

        [TestMethod]
        public void OnStatusRefreshed_FiresOnEveryReApplication_BelowMaxStacks()
        {
            _manager.Statuses.RegisterStatus(new Status("Burning", duration: 5f, maxStacks: 5));

            _manager.Statuses.ApplyStatus(_entity, "Burning"); // applied
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // refreshed (stacks 1->2)
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // refreshed (stacks 2->3)

            CollectionAssert.AreEqual(
                new[] { "applied:Burning", "refreshed:Burning", "refreshed:Burning" },
                _eventLog);
            Assert.AreEqual(3, _manager.Statuses.GetStacks(_entity, "Burning"));
        }

        [TestMethod]
        public void OnStatusRefreshed_FiresAtMaxStacks_EvenWithNoCounterChange()
        {
            // Critical: at MaxStacks, OnStatusStacked does NOT fire (counter unchanged)
            // but OnStatusRefreshed DOES fire (duration is still refreshed).
            _manager.Statuses.RegisterStatus(new Status("Burning", duration: 5f, maxStacks: 2));

            _manager.Statuses.ApplyStatus(_entity, "Burning"); // applied (stacks=1)
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // refreshed (stacks 1->2, hit max)
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // refreshed (at max - counter unchanged but duration refreshes)
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // refreshed again

            Assert.AreEqual(2, _manager.Statuses.GetStacks(_entity, "Burning"));
            CollectionAssert.AreEqual(
                new[] { "applied:Burning", "refreshed:Burning", "refreshed:Burning", "refreshed:Burning" },
                _eventLog);
        }

        [TestMethod]
        public void OnStatusRefreshed_DoesNotFireOnRemoveStacksPartialDecrement()
        {
            _manager.Statuses.RegisterStatus(new Status("Burning", duration: 5f, maxStacks: 5));
            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");

            int refreshedAfter = 0;
            _manager.Statuses.OnStatusRefreshed += (_, _) => refreshedAfter++;

            ((IStackOperations)_manager.Statuses).RemoveStacks(_entity, "Burning", 1);

            Assert.AreEqual(0, refreshedAfter,
                "Partial RemoveStacks decrements the counter but does NOT touch RemainingDuration; OnStatusRefreshed must not fire.");
        }

        // -------- OnRefreshEffects from status definition --------

        [TestMethod]
        public void OnRefreshEffects_FireOnRefresh_NotOnFirstApplication()
        {
            // Build a status whose OnRefresh fires a -10 Health effect.
            var status = StatusBuilder.Create("Burning")
                .WithDuration(5f)
                .Stackable(3)
                .OnRefresh(EffectBuilder.Create("burnRefresh").Instant().AdjustStat("Health", -10f))
                .Build();
            _manager.Statuses.RegisterStatus(status);

            // First apply: birth, no OnRefresh.
            _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(100f, _entity.GetStat("Health").CurrentValue, 0.001f,
                "OnRefresh must NOT fire on first application.");

            // Re-apply: stacks 1->2, OnRefresh fires once -> -10 Health.
            _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(90f, _entity.GetStat("Health").CurrentValue, 0.001f);

            // Re-apply: stacks 2->3, OnRefresh fires again -> -10 more.
            _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(80f, _entity.GetStat("Health").CurrentValue, 0.001f);
        }

        [TestMethod]
        public void OnRefreshEffects_FireAtMaxStacks_StandInFlamePattern()
        {
            // Classic use case: stand-in-flame. Burning maxStacks=1 means every
            // re-application is at-max, with no stack change, but OnRefresh
            // bursts damage on every step into the flame.
            var status = StatusBuilder.Create("Burning")
                .WithDuration(5f)
                .Stackable(1)
                .OnRefresh(EffectBuilder.Create("flameStep").Instant().AdjustStat("Health", -5f))
                .Build();
            _manager.Statuses.RegisterStatus(status);

            _manager.Statuses.ApplyStatus(_entity, "Burning"); // birth, no refresh
            for (int i = 0; i < 4; i++) _manager.Statuses.ApplyStatus(_entity, "Burning"); // 4 refreshes at max

            Assert.AreEqual(1, _manager.Statuses.GetStacks(_entity, "Burning"));
            Assert.AreEqual(80f, _entity.GetStat("Health").CurrentValue, 0.001f,
                "At MaxStacks=1 every re-application fires OnRefresh: 4 * -5 = -20 from the initial 100.");
        }

        [TestMethod]
        public void MultipleOnRefreshEffects_AllFirePerRefresh()
        {
            var status = StatusBuilder.Create("Burning")
                .WithDuration(5f)
                .Stackable(3)
                .OnRefresh(EffectBuilder.Create("a").Instant().AdjustStat("Health", -3f))
                .OnRefresh(EffectBuilder.Create("b").Instant().AdjustStat("Health", -7f))
                .Build();
            _manager.Statuses.RegisterStatus(status);

            _manager.Statuses.ApplyStatus(_entity, "Burning"); // birth, no refresh
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // refresh -> -3 + -7 = -10

            Assert.AreEqual(90f, _entity.GetStat("Health").CurrentValue, 0.001f);
        }

        // -------- Backward-compatibility regression --------

        [TestMethod]
        public void StatusWithoutOnRefresh_BehavesLikeV10()
        {
            // No OnRefresh on this status. Re-applying should change nothing
            // beyond the existing v1.0/v1.1 stack increment + duration refresh.
            _manager.Statuses.RegisterStatus(new Status("Burning", duration: 5f, maxStacks: 3));

            _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.ApplyStatus(_entity, "Burning");
            _manager.Statuses.ApplyStatus(_entity, "Burning");

            // Health untouched (no OnRefresh effects defined).
            Assert.AreEqual(100f, _entity.GetStat("Health").CurrentValue, 0.001f);
            Assert.AreEqual(3, _manager.Statuses.GetStacks(_entity, "Burning"));
            // OnStatusRefreshed still fires (it's the EVENT, not the EFFECTS) - that's expected.
            // The "no behaviour change" promise is about user-visible state, not internal events.
        }

        /// <summary>
        /// External implementation of <see cref="IStatus"/> that omits
        /// <c>OnRefreshEffects</c> property by returning null. The engine's
        /// manager-side handler must tolerate this gracefully.
        /// </summary>
        private sealed class LegacyExternalStatus : IStatus
        {
            public string Key => "Legacy";
            public string[] Tags => new string[0];
            public float Duration => 5f;
            public int MaxStacks => 3;
            public IEffect[] OnApplyEffects => new IEffect[0];
            public IEffect[] OnTickEffects => new IEffect[0];
            public IEffect[] OnRemoveEffects => new IEffect[0];
            public IEffect[] OnRefreshEffects => null; // explicit null
            public float TickInterval => 1f;
        }

        [TestMethod]
        public void ExternalStatus_WithNullOnRefreshEffects_DoesNotThrow()
        {
            _manager.Statuses.RegisterStatus(new LegacyExternalStatus());

            _manager.Statuses.ApplyStatus(_entity, "Legacy");
            _manager.Statuses.ApplyStatus(_entity, "Legacy"); // refresh path - manager handler must null-guard

            Assert.AreEqual(2, _manager.Statuses.GetStacks(_entity, "Legacy"));
        }
    }
}
