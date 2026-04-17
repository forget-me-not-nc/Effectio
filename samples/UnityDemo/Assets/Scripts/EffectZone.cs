using UnityEngine;
using Effectio.Builders;

namespace EffectioDemo
{
    /// <summary>The kind of effect a coloured pad applies when stepped on.</summary>
    public enum ZoneKind
    {
        Fire,
        Water,
        Lightning,
        Heal,
        Sanctuary,
        Poison,
        Haste,
        Slow,
        Bleed,
        Adrenaline,
    }

    /// <summary>
    /// A coloured trigger pad on the floor. When a <see cref="CharacterStats"/>
    /// walks into it, the corresponding effect / status is applied through
    /// Effectio. Fire-and-forget: nothing else happens until the player walks
    /// in again.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EffectZone : MonoBehaviour
    {
        [SerializeField] ZoneKind _kind;

        public ZoneKind Kind => _kind;

        public void Configure(ZoneKind kind) => _kind = kind;

        void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var stats = other.GetComponent<CharacterStats>();
            if (stats == null || stats.Entity == null || !stats.IsAlive) return;

            var manager = EffectioWorld.Instance.Manager;
            var entity = stats.Entity;

            switch (_kind)
            {
                case ZoneKind.Fire:
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_fire").Instant().ApplyStatus("Burning").Build());
                    break;

                case ZoneKind.Water:
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_water").Instant().ApplyStatus("Wet").Build());
                    break;

                case ZoneKind.Lightning:
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_lightning").Instant().ApplyStatus("Charged").Build());
                    break;

                case ZoneKind.Heal:
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_heal").Instant().AdjustStat("Health", 20f).Build());
                    break;

                case ZoneKind.Sanctuary:
                    // Cleanse: remove every elemental + debuff status the demo defines.
                    manager.Statuses.RemoveStatus(entity, "Burning");
                    manager.Statuses.RemoveStatus(entity, "Wet");
                    manager.Statuses.RemoveStatus(entity, "Charged");
                    manager.Statuses.RemoveStatus(entity, "Stunned");
                    manager.Statuses.RemoveStatus(entity, "Bleeding");
                    manager.Statuses.RemoveStatus(entity, "Slowed");
                    manager.Statuses.RemoveStatus(entity, "Weakened");
                    break;

                case ZoneKind.Poison:
                    // Multi-effect zone: periodic damage + Slowed + Weakened all at once.
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_poison_dot")
                            .Periodic(duration: 8f, tickInterval: 1f)
                            .AdjustStat("Health", -2f)
                            .Build());
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_poison_slow").Instant().ApplyStatus("Slowed").Build());
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_poison_weak").Instant().ApplyStatus("Weakened").Build());
                    break;

                case ZoneKind.Haste:
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_haste").Instant().ApplyStatus("Hasted").Build());
                    break;

                case ZoneKind.Slow:
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_slow").Instant().ApplyStatus("Slowed").Build());
                    break;

                case ZoneKind.Bleed:
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_bleed").Instant().ApplyStatus("Bleeding").Build());
                    break;

                case ZoneKind.Adrenaline:
                    // Triggered effect: arms a 60-second window during which, the moment
                    // Health drops below 30, the action fires once (+25 HP).
                    manager.Effects.ApplyEffect(entity,
                        EffectBuilder.Create("zone_adrenaline")
                            .Triggered(duration: 60f)
                            .AdjustStat("Health", 25f)
                            .WhenStatBelow("Health", 30f)
                            .Build());
                    break;
            }

            Debug.Log($"{stats.DisplayName} stepped on the {_kind} pad.");
        }
    }
}

