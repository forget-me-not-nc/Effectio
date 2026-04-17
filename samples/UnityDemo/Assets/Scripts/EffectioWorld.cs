using UnityEngine;
using Effectio.Core;
using Effectio.Builders;
using Effectio.Modifiers;
using Effectio.Statuses;

namespace EffectioDemo
{
    /// <summary>
    /// Scene-wide singleton that owns the single <see cref="EffectioManager"/> and drives
    /// <see cref="EffectioManager.Tick"/> from Unity's Update loop. Also registers all
    /// statuses and reactions the demo uses, in one place.
    /// </summary>
    public class EffectioWorld : MonoBehaviour
    {
        public static EffectioWorld Instance { get; private set; }
        public EffectioManager Manager { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Manager = new EffectioManager();
            RegisterCatalog();

            // Handy for the tutorial: log every reaction that fires.
            Manager.Reactions.OnReactionTriggered += (entity, reaction) =>
                Debug.Log($"[Effectio] Reaction '{reaction.Key}' fired on '{entity.Id}'.");
        }

        void Update()
        {
            // One call per frame drives the whole simulation.
            Manager.Tick(Time.deltaTime);
        }

        void RegisterCatalog()
        {
            // --- Statuses -------------------------------------------------

            // Burning: stackable up to 3, ticks -5 HP per second per stack lifetime.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Burning")
                .WithTags("Fire", "Elemental")
                .WithDuration(5f).Stackable(3).WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("burn_tick").Instant().AdjustStat("Health", -5f))
                .Build());

            // Wet: no damage on its own but several reactions key off it.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Wet")
                .WithTags("Water", "Elemental")
                .WithDuration(5f)
                .Build());

            // Charged: electrical status that combines explosively with Wet or Burning.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Charged")
                .WithTags("Lightning", "Elemental")
                .WithDuration(5f)
                .Build());

            // Stunned: short-lived debuff applied by Vaporize / Apocalypse.
            Manager.Statuses.RegisterStatus(new Status("Stunned", duration: 2f));

            // Bleeding: stackable up to 5, ticks -1 HP per second.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Bleeding")
                .WithTags("Physical")
                .WithDuration(8f).Stackable(5).WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("bleed_tick").Instant().AdjustStat("Health", -1f))
                .Build());

            // Hasted: +50% Speed for 5s. Modifier is undone when the Timed effect
            // expires; status itself just exists so the HUD can show it.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Hasted")
                .WithDuration(5f)
                .OnApply(EffectBuilder.Create("haste_mod").Timed(5f)
                    .ApplyModifier("Speed",
                        e => new MultiplicativeModifier(e.Key + "_mod", 1.5f, e.Duration, e.Key)))
                .Build());

            // Slowed: -50% Speed for 5s.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Slowed")
                .WithDuration(5f)
                .OnApply(EffectBuilder.Create("slow_mod").Timed(5f)
                    .ApplyModifier("Speed",
                        e => new MultiplicativeModifier(e.Key + "_mod", 0.5f, e.Duration, e.Key)))
                .Build());

            // Weakened: -30% Damage for 5s.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Weakened")
                .WithDuration(5f)
                .OnApply(EffectBuilder.Create("weak_mod").Timed(5f)
                    .ApplyModifier("Damage",
                        e => new MultiplicativeModifier(e.Key + "_mod", 0.7f, e.Duration, e.Key)))
                .Build());

            // Powered: +50% Damage for 5s, applied by Overload.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Powered")
                .WithDuration(5f)
                .OnApply(EffectBuilder.Create("powered_buff").Timed(5f)
                    .ApplyModifier("Damage",
                        e => new MultiplicativeModifier(e.Key + "_mod", 1.5f, e.Duration, e.Key)))
                .Build());

            // --- Reactions ------------------------------------------------
            // ORDER MATTERS: ReactionEngine fires the FIRST matching reaction and
            // then consumes its statuses. Register the most-specific (most required
            // statuses) reaction first so it gets priority over its 2-status subsets.

            // 5-condition ultimate: Burning + Wet + Charged + Bleeding + Weakened.
            // Hard to trigger, devastating when it does. Demonstrates many-condition
            // reactions (the engine accepts any RequireStatuses arity).
            Manager.Reactions.RegisterReaction(ReactionBuilder.Create("Apocalypse")
                .RequireStatuses("Burning", "Wet", "Charged", "Bleeding", "Weakened")
                .ConsumesStatuses()
                .AdjustStat("Health", -100f)
                .ApplyStatus("Stunned")
                .Build());

            // Burning + Wet = Vaporize: 40 burst damage, Stunned 2s, consumes both.
            Manager.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .AdjustStat("Health", -40f)
                .ApplyStatus("Stunned")
                .Build());

            // Wet + Charged = Electrocuted: 30 burst damage, consumes both.
            Manager.Reactions.RegisterReaction(ReactionBuilder.Create("Electrocuted")
                .RequireStatuses("Wet", "Charged")
                .ConsumesStatuses()
                .AdjustStat("Health", -30f)
                .Build());

            // Burning + Charged = Overload: 25 damage + Powered buff, consumes both.
            Manager.Reactions.RegisterReaction(ReactionBuilder.Create("Overload")
                .RequireStatuses("Burning", "Charged")
                .ConsumesStatuses()
                .AdjustStat("Health", -25f)
                .ApplyStatus("Powered")
                .Build());

            // Wet + Slowed = Frostbite: 20 burst damage, consumes both.
            Manager.Reactions.RegisterReaction(ReactionBuilder.Create("Frostbite")
                .RequireStatuses("Wet", "Slowed")
                .ConsumesStatuses()
                .AdjustStat("Health", -20f)
                .Build());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
