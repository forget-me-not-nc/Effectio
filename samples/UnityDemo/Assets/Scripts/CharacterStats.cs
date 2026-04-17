using UnityEngine;
using Effectio.Entities;
using Effectio.Stats;

namespace EffectioDemo
{
    /// <summary>
    /// Binds this <see cref="GameObject"/> to an Effectio entity and exposes the stats the
    /// rest of the demo queries (<c>Health</c>, <c>Damage</c>). When Health hits 0 the
    /// GameObject destroys itself.
    /// </summary>
    public class CharacterStats : MonoBehaviour
    {
        [SerializeField] string _displayName = "Character";
        [SerializeField] float _maxHealth = 100f;
        [SerializeField] float _baseDamage = 15f;
        [SerializeField] float _baseSpeed = 6f;
        [SerializeField] float _minHealth = 0f;

        public string DisplayName => _displayName;
        public IEffectioEntity Entity { get; private set; }

        public float Health => Entity.GetStat("Health").CurrentValue;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => Health > _minHealth;

        /// <summary>
        /// Configure this character at runtime. Must be called before <c>Start</c>
        /// runs (so typically immediately after <c>AddComponent</c>).
        /// <paramref name="minHealth"/> &gt; 0 keeps the character alive forever
        /// (handy for demos so the player cannot die from environmental damage).
        /// </summary>
        public void Configure(string displayName, float maxHealth, float baseDamage,
                              float minHealth = 0f, float baseSpeed = 6f)
        {
            _displayName = displayName;
            _maxHealth = maxHealth;
            _baseDamage = baseDamage;
            _minHealth = minHealth;
            _baseSpeed = baseSpeed;
        }

        void Start()
        {
            if (EffectioWorld.Instance == null)
            {
                Debug.LogError("No EffectioWorld in scene. Add the 'DemoBootstrap' component to a GameObject.");
                enabled = false;
                return;
            }

            var world = EffectioWorld.Instance.Manager;

            Entity = world.CreateEntity(_displayName + "_" + GetInstanceID());
            Entity.AddStat(new Stat("Health", _maxHealth, _minHealth, _maxHealth));
            Entity.AddStat(new Stat("Damage", _baseDamage));
            Entity.AddStat(new Stat("Speed", _baseSpeed));

            Entity.GetStat("Health").OnValueChanged += (_, _, newHp) =>
            {
                if (newHp <= _minHealth && isActiveAndEnabled) Die();
            };
        }

        void OnDestroy()
        {
            // Keep the simulation clean when the GameObject goes away.
            if (EffectioWorld.Instance != null && Entity != null)
                EffectioWorld.Instance.Manager.RemoveEntity(Entity.Id);
        }

        void Die()
        {
            Debug.Log($"{_displayName} died.");
            Destroy(gameObject);
        }
    }
}
