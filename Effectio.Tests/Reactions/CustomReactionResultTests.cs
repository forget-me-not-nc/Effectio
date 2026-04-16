using Microsoft.VisualStudio.TestTools.UnitTesting;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Reactions;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Tests.Reactions
{
    [TestClass]
    public class CustomReactionResultTests
    {
        /// <summary>Test-only result that flips a flag — proves the engine calls Execute polymorphically.</summary>
        private sealed class FlagResult : IReactionResult
        {
            public ReactionResultType Type => ReactionResultType.Custom;
            public string TargetKey => null;
            public float Value => 0f;
            public bool Fired { get; private set; }

            public void Execute(in ReactionResultContext ctx) => Fired = true;
        }

        private EffectioManager _manager;

        [TestInitialize]
        public void Setup()
        {
            _manager = new EffectioManager();
            _manager.Statuses.RegisterStatus(new Status("Burning"));
            _manager.Statuses.RegisterStatus(new Status("Wet"));
            _manager.Statuses.RegisterStatus(new Status("Vaporized"));
        }

        [TestMethod]
        public void ConcreteResultTypes_ExecuteCorrectly()
        {
            var reaction = ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .WithResult(new ApplyStatusResult("Vaporized"))
                .WithResult(new AdjustStatResult("Health", -25f))
                .Build();

            _manager.Reactions.RegisterReaction(reaction);

            var p = _manager.CreateEntity("p");
            p.AddStat(new Stat("Health", 100f, 0f, 200f));

            _manager.Statuses.ApplyStatus(p, "Burning");
            _manager.Statuses.ApplyStatus(p, "Wet");

            Assert.IsTrue(p.HasStatus("Vaporized"));
            Assert.AreEqual(75f, p.GetStat("Health").CurrentValue);
            Assert.IsFalse(p.HasStatus("Burning"));
            Assert.IsFalse(p.HasStatus("Wet"));
        }

        [TestMethod]
        public void CustomResult_Execute_IsCalledByEngine()
        {
            var flag = new FlagResult();

            var reaction = ReactionBuilder.Create("Marker")
                .RequireStatuses("Burning", "Wet")
                .Persists()
                .WithResult(flag)
                .Build();

            _manager.Reactions.RegisterReaction(reaction);
            var p = _manager.CreateEntity("p");

            _manager.Statuses.ApplyStatus(p, "Burning");
            _manager.Statuses.ApplyStatus(p, "Wet");

            Assert.IsTrue(flag.Fired);
        }

        [TestMethod]
        public void RemoveStatusResult_StripsStatus()
        {
            var reaction = ReactionBuilder.Create("Dispel")
                .RequireStatuses("Burning", "Wet")
                .Persists()
                .WithResult(new RemoveStatusResult("Burning"))
                .Build();

            _manager.Reactions.RegisterReaction(reaction);
            var p = _manager.CreateEntity("p");

            _manager.Statuses.ApplyStatus(p, "Burning");
            _manager.Statuses.ApplyStatus(p, "Wet");

            Assert.IsFalse(p.HasStatus("Burning"));
            Assert.IsTrue(p.HasStatus("Wet"));
        }
    }
}
