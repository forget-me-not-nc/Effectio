using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Core;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Tests.Statuses
{
    /// <summary>
    /// Locks down v1.x stack-expiration semantics: all stacks of a status
    /// share one combined <c>RemainingDuration</c>; stacking refreshes that
    /// duration to <see cref="IStatus.Duration"/>; expiration removes the
    /// entire status (all stacks at once) and fires <c>OnStatusExpired</c>
    /// exactly once. <see cref="IStackOperations.RemoveStacks"/> does NOT
    /// touch the duration; remaining stacks keep the in-flight value.
    /// </summary>
    /// <remarks>
    /// These tests describe the contract that v1.x callers can rely on. A
    /// future v2.0 candidate may distinguish individual stacks (each with
    /// its own expiration); when that lands these tests become the v1
    /// regression suite proving that the legacy combined-counter behaviour
    /// is still selectable / opt-in.
    /// </remarks>
    [TestClass]
    public class StackExpirationTests
    {
        private EffectioManager _manager;
        private IEffectioEntity _entity;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
            // duration: 1s, maxStacks: 5, tickInterval: large (so OnTickEffects don't interfere)
            _manager.Statuses.RegisterStatus(new Status(
                "Burning", duration: 1f, maxStacks: 5, tickInterval: 1000f));
            _manager.Statuses.RegisterStatus(new Status(
                "Eternal", duration: -1f, maxStacks: 5));
            _entity = _manager.CreateEntity("p");
        }

        // -------- Single-stack lifecycle --------

        [TestMethod]
        public void SingleStack_ExpiresAfterDuration_FiresOnStatusExpiredOnce()
        {
            int expiredCount = 0;
            string expiredKey = null;
            _manager.Statuses.OnStatusExpired += (_, key) => { expiredCount++; expiredKey = key; };

            _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(1, _manager.Statuses.GetStacks(_entity, "Burning"));

            // Tick past duration. Engine decrements first, then expires on next Tick where remaining <= 0.
            _manager.Tick(0.6f); // 0.4 left
            Assert.IsTrue(_entity.HasStatus("Burning"), "Should still be alive at 0.4s remaining.");
            _manager.Tick(0.6f); // would be -0.2 -> expires

            Assert.IsFalse(_entity.HasStatus("Burning"));
            Assert.AreEqual(0, _manager.Statuses.GetStacks(_entity, "Burning"));
            Assert.AreEqual(1, expiredCount, "OnStatusExpired must fire exactly once.");
            Assert.AreEqual("Burning", expiredKey);
        }

        // -------- Multi-stack expiration --------

        [TestMethod]
        public void MultipleStacks_ExpireTogether_AsOneEvent()
        {
            int expiredCount = 0;
            _manager.Statuses.OnStatusExpired += (_, _) => expiredCount++;

            // Build 3 stacks; each ApplyStatus also refreshes duration to 1.0s.
            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(3, _manager.Statuses.GetStacks(_entity, "Burning"));

            _manager.Tick(0.6f);
            Assert.IsTrue(_entity.HasStatus("Burning"), "All 3 stacks share one duration; should still be alive at 0.4s remaining.");
            _manager.Tick(0.6f); // expires

            Assert.IsFalse(_entity.HasStatus("Burning"), "All stacks expire together; the whole status is gone.");
            Assert.AreEqual(0, _manager.Statuses.GetStacks(_entity, "Burning"));
            Assert.AreEqual(1, expiredCount, "Combined-counter expiration fires OnStatusExpired exactly once, not once per stack.");
        }

        // -------- Stacking refreshes duration --------

        [TestMethod]
        public void Stacking_RefreshesDurationToFull()
        {
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // RemainingDuration = 1.0
            _manager.Tick(0.7f); // RemainingDuration = 0.3

            // Apply again - increments stacks AND refreshes duration to 1.0
            _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(2, _manager.Statuses.GetStacks(_entity, "Burning"));

            // Tick another 0.7s. Original 1.0s would have expired by 1.4s elapsed.
            // With refresh, remaining is 1.0 - 0.7 = 0.3, still alive.
            _manager.Tick(0.7f);
            Assert.IsTrue(_entity.HasStatus("Burning"), "Stacking refreshed the timer; status survives past the original would-have-expired moment.");

            // Tick past the refreshed duration.
            _manager.Tick(0.5f);
            Assert.IsFalse(_entity.HasStatus("Burning"));
        }

        [TestMethod]
        public void ApplyStatusAtMaxStacks_StillRefreshesDuration()
        {
            // Climb to MaxStacks (5). Then a 6th apply: stacks stay at 5 (no
            // OnStatusStacked) but RemainingDuration is still refreshed.
            for (int i = 0; i < 5; i++) _manager.Statuses.ApplyStatus(_entity, "Burning");
            Assert.AreEqual(5, _manager.Statuses.GetStacks(_entity, "Burning"));

            _manager.Tick(0.7f); // RemainingDuration = 0.3, still at MaxStacks

            int stackedCount = 0;
            ((IStackOperations)_manager.Statuses).OnStatusStacked += (_, _) => stackedCount++;

            _manager.Statuses.ApplyStatus(_entity, "Burning"); // at max -> no increment, no OnStatusStacked, but refresh
            Assert.AreEqual(5, _manager.Statuses.GetStacks(_entity, "Burning"));
            Assert.AreEqual(0, stackedCount, "OnStatusStacked must not fire at MaxStacks (counter unchanged).");

            // 0.5s after the refresh - would have expired at 1.2s elapsed (no refresh) but is still alive.
            _manager.Tick(0.5f);
            Assert.IsTrue(_entity.HasStatus("Burning"), "Refresh at MaxStacks extended life past the would-have-expired moment.");
        }

        // -------- RemoveStacks does NOT touch duration --------

        [TestMethod]
        public void RemoveStacks_PartialDecrement_DoesNotResetDuration()
        {
            var ops = (IStackOperations)_manager.Statuses;

            _manager.Statuses.ApplyStatus(_entity, "Burning"); // 1
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // 2
            _manager.Statuses.ApplyStatus(_entity, "Burning"); // 3, RemainingDuration = 1.0

            _manager.Tick(0.6f); // RemainingDuration = 0.4
            ops.RemoveStacks(_entity, "Burning", 1); // 2 stacks left, duration UNCHANGED at 0.4

            // If RemoveStacks reset duration, we'd survive past 0.4s. It does not - so 0.5s tick expires us.
            _manager.Tick(0.5f);
            Assert.IsFalse(_entity.HasStatus("Burning"),
                "Partial RemoveStacks must NOT reset RemainingDuration; remaining stacks expire on the original timer.");
        }

        // -------- Permanent status (Duration = -1) --------

        [TestMethod]
        public void PermanentStatus_NeverExpires_RegardlessOfStacks()
        {
            int expiredCount = 0;
            _manager.Statuses.OnStatusExpired += (_, _) => expiredCount++;

            for (int i = 0; i < 3; i++) _manager.Statuses.ApplyStatus(_entity, "Eternal");

            // Tick a long time.
            for (int i = 0; i < 100; i++) _manager.Tick(1f);

            Assert.IsTrue(_entity.HasStatus("Eternal"), "Duration = -1 means permanent; should never expire.");
            Assert.AreEqual(3, _manager.Statuses.GetStacks(_entity, "Eternal"));
            Assert.AreEqual(0, expiredCount);
        }
    }
}
