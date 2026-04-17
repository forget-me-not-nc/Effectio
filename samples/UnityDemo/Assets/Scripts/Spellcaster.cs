using UnityEngine;
using Effectio.Builders;

namespace EffectioDemo
{
    /// <summary>
    /// Minimal spellcaster: press F to fire (applies Burning), W to splash water
    /// (applies Wet), H to heal (+20 HP) on the currently-assigned target.
    /// Fire+Water on the same target triggers the Vaporize reaction automatically.
    /// </summary>
    public class Spellcaster : MonoBehaviour
    {
        [SerializeField] CharacterStats _target;

        public void SetTarget(CharacterStats target) => _target = target;

        void Update()
        {
            if (_target == null || !_target.IsAlive) return;

            if (Input.GetKeyDown(KeyCode.F)) CastFire();
            if (Input.GetKeyDown(KeyCode.W)) CastWater();
            if (Input.GetKeyDown(KeyCode.H)) CastHeal();
        }

        void CastFire()
        {
            // "Instant" effects just apply their action once and immediately finish.
            // ApplyStatus -> the status-engine handles Burning's duration + tick damage.
            var fire = EffectBuilder.Create("fireball").Instant().ApplyStatus("Burning").Build();
            EffectioWorld.Instance.Manager.Effects.ApplyEffect(_target.Entity, fire);
            Debug.Log($"Cast Fire on {_target.DisplayName} (HP {_target.Health:0})");
        }

        void CastWater()
        {
            var water = EffectBuilder.Create("splash").Instant().ApplyStatus("Wet").Build();
            EffectioWorld.Instance.Manager.Effects.ApplyEffect(_target.Entity, water);
            Debug.Log($"Cast Water on {_target.DisplayName} (HP {_target.Health:0})");
        }

        void CastHeal()
        {
            var heal = EffectBuilder.Create("minor_heal").Instant().AdjustStat("Health", 20f).Build();
            EffectioWorld.Instance.Manager.Effects.ApplyEffect(_target.Entity, heal);
            Debug.Log($"Healed {_target.DisplayName} (HP {_target.Health:0})");
        }
    }
}
