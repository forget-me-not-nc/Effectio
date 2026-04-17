using UnityEngine;
using Effectio.Core;
using Effectio.Builders;
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

            // Burning: elemental fire tag, ticks -5 HP per second for 5 seconds.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Burning")
                .WithTags("Fire", "Elemental")
                .WithDuration(5f).WithTickInterval(1f)
                .OnTick(EffectBuilder.Create("burn_tick").Instant().AdjustStat("Health", -5f))
                .Build());

            // Wet: no damage on its own but the reaction below cares about it.
            Manager.Statuses.RegisterStatus(StatusBuilder.Create("Wet")
                .WithTags("Water", "Elemental")
                .WithDuration(5f)
                .Build());

            // Stunned: a short-lived debuff the Vaporize reaction applies.
            Manager.Statuses.RegisterStatus(new Status("Stunned", duration: 2f));

            // --- Reactions ------------------------------------------------

            // Fire + Water on the same entity = Vaporize: consume both statuses,
            // deal 40 burst damage, apply Stunned for 2s.
            Manager.Reactions.RegisterReaction(ReactionBuilder.Create("Vaporize")
                .RequireStatuses("Burning", "Wet")
                .ConsumesStatuses()
                .AdjustStat("Health", -40f)
                .ApplyStatus("Stunned")
                .Build());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
