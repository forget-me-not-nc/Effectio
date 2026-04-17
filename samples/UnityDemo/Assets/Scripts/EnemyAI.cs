using UnityEngine;
using Effectio.Builders;

namespace EffectioDemo
{
    /// <summary>
    /// Walks toward the nearest <see cref="PlayerMovement"/> at this entity's
    /// Speed stat. When within contact range, applies <c>Burning</c> to the
    /// player on a 1-second cooldown.
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        [SerializeField] float _contactRange = 1.6f;
        [SerializeField] float _attackCooldown = 1f;

        CharacterStats _stats;
        Transform _player;
        CharacterStats _playerStats;
        float _nextAttackAt;

        void Start()
        {
            _stats = GetComponent<CharacterStats>();
            var player = FindObjectOfType<PlayerMovement>();
            if (player != null)
            {
                _player = player.transform;
                _playerStats = player.GetComponent<CharacterStats>();
            }
        }

        void Update()
        {
            if (_player == null || _stats == null || _stats.Entity == null || !_stats.IsAlive) return;
            if (_playerStats == null || !_playerStats.IsAlive) return;

            var to = _player.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // Walk toward the player at this entity's Speed stat.
            float speed = _stats.Entity.HasStat("Speed")
                ? _stats.Entity.GetStat("Speed").CurrentValue
                : 3f;

            if (dist > _contactRange && dist > 0.001f)
            {
                transform.position += (to / dist) * (speed * Time.deltaTime);
            }
            else if (Time.time >= _nextAttackAt)
            {
                // In contact: light the player on Burning.
                EffectioWorld.Instance.Manager.Effects.ApplyEffect(
                    _playerStats.Entity,
                    EffectBuilder.Create("enemy_touch_burn").Instant().ApplyStatus("Burning").Build());
                _nextAttackAt = Time.time + _attackCooldown;
            }
        }
    }
}
