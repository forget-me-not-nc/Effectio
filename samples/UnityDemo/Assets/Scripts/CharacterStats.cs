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

        public string DisplayName => _displayName;
        public IEffectioEntity Entity { get; private set; }

        public float Health => Entity.GetStat("Health").CurrentValue;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => Health > 0f;

        /// <summary>
        /// Configure this character at runtime. Must be called before <c>Start</c>
        /// runs (so typically immediately after <c>AddComponent</c>).
        /// </summary>
        public void Configure(string displayName, float maxHealth, float baseDamage)
        {
            _displayName = displayName;
            _maxHealth = maxHealth;
            _baseDamage = baseDamage;
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

            // Each character gets a unique entity keyed by instance id so two
            // enemies can safely share a display name.
            Entity = world.CreateEntity(_displayName + "_" + GetInstanceID());
            Entity.AddStat(new Stat("Health", _maxHealth, 0f, _maxHealth));
            Entity.AddStat(new Stat("Damage", _baseDamage));

            // Effectio raises OnValueChanged whenever a stat actually changes;
            // perfect place to drive Unity-side reactions like death / UI.
            Entity.GetStat("Health").OnValueChanged += (_, _, newHp) =>
            {
                if (newHp <= 0f && isActiveAndEnabled) Die();
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
