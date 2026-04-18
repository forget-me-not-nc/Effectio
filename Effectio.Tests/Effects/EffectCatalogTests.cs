using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Effects;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Effects
{
    /// <summary>
    /// Coverage for the v1.1 effect catalog (<see cref="IEffectCatalog"/>) and the
    /// <c>OnApplyEffect</c> wiring it makes possible. Pre-v1.1 the
    /// <c>ReactionBuilder.ApplyEffect(string)</c> result was a silent no-op because
    /// the reaction engine's <c>OnApplyEffect</c> callback was never wired.
    /// </summary>
    [TestClass]
    public class EffectCatalogTests
    {
        private EffectioManager _manager;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
        }

        // -------- Catalog basics --------

        [TestMethod]
        public void RegisterAndTryGet_Roundtrips()
        {
            var effect = new Effect("Heal", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: 25f);
            _manager.EffectCatalog.RegisterEffect(effect);

            Assert.IsTrue(_manager.EffectCatalog.TryGetEffect("Heal", out var resolved));
            Assert.AreSame(effect, resolved);
        }

        [TestMethod]
        public void TryGetEffect_ReturnsFalseForUnknownKey()
        {
            Assert.IsFalse(_manager.EffectCatalog.TryGetEffect("DoesNotExist", out var resolved));
            Assert.IsNull(resolved);
        }

        [TestMethod]
        public void RegisterEffect_DuplicateKeyReplacesPrevious()
        {
            // Mirrors StatusEngine.RegisterStatus: last registration wins, no throw.
            var first = new Effect("X", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: 1f);
            var second = new Effect("X", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: 99f);

            _manager.EffectCatalog.RegisterEffect(first);
            _manager.EffectCatalog.RegisterEffect(second);

            Assert.IsTrue(_manager.EffectCatalog.TryGetEffect("X", out var resolved));
            Assert.AreSame(second, resolved);
        }

        [TestMethod]
        public void RegisteredEffects_ExposesAll()
        {
            var a = new Effect("A", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: 1f);
            var b = new Effect("B", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: 2f);
            _manager.EffectCatalog.RegisterEffect(a);
            _manager.EffectCatalog.RegisterEffect(b);

            var keys = _manager.EffectCatalog.RegisteredEffects.Select(e => e.Key).OrderBy(k => k).ToArray();
            CollectionAssert.AreEqual(new[] { "A", "B" }, keys);
        }

        // -------- ReactionBuilder.ApplyEffect(string) wiring (the bug fix) --------

        [TestMethod]
        public void ReactionApplyEffect_ResolvesThroughCatalogAndExecutes()
        {
            // Pre-v1.1 this test would fail: Health stays at 100 because OnApplyEffect
            // was never wired. v1.1 wires it through the catalog so the effect runs.
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Health", 100f, 0f, 500f));

            var damageBurst = new Effect("DamageBurst", EffectType.Instant, EffectActionType.AdjustStat, "Health", value: -30f);
            _manager.EffectCatalog.RegisterEffect(damageBurst);

            _manager.Statuses.RegisterStatus(new Status("Burning"));
            _manager.Statuses.RegisterStatus(new Status("Wet"));
            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .ApplyEffect("DamageBurst")
                .Build());

            _manager.Statuses.ApplyStatus(entity, "Burning");
            _manager.Statuses.ApplyStatus(entity, "Wet");

            Assert.AreEqual(70f, entity.GetStat("Health").CurrentValue,
                "Reaction's ApplyEffect(string) should resolve through the catalog and apply the effect.");
        }

        [TestMethod]
        public void ReactionApplyEffect_UnknownKeyDoesNotThrowAndOtherResultsStillExecute()
        {
            // A reaction with two results: one ApplyEffect referencing a missing key,
            // one direct AdjustStat. The missing key is logged and skipped; the
            // AdjustStat still runs.
            var entity = _manager.CreateEntity("p");
            entity.AddStat(new Stat("Health", 100f, 0f, 500f));

            _manager.Statuses.RegisterStatus(new Status("Burning"));
            _manager.Statuses.RegisterStatus(new Status("Wet"));
            _manager.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .ApplyEffect("DoesNotExist")  // unknown key, should warn-log and skip
                .AdjustStat("Health", -10f)   // sibling result, should still execute
                .Build());

            _manager.Statuses.ApplyStatus(entity, "Burning");
            _manager.Statuses.ApplyStatus(entity, "Wet");

            Assert.AreEqual(90f, entity.GetStat("Health").CurrentValue,
                "AdjustStat result should still execute even though ApplyEffect's key is unknown.");
        }
    }
}
