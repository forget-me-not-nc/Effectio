using UnityEngine;

namespace EffectioDemo
{
    /// <summary>
    /// One-component demo bootstrapper. Drop this on a single empty GameObject in a
    /// fresh scene, press Play, and everything else (EffectioWorld, player, enemy,
    /// HUD) is created for you. The point is zero scene wiring, so the interesting
    /// bit is the Effectio code itself, not Unity setup.
    /// </summary>
    public class DemoBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // EffectioWorld owns the EffectioManager and drives Tick. Singleton.
            var worldGo = new GameObject("EffectioWorld");
            worldGo.AddComponent<EffectioWorld>();

            // HUD: read-only IMGUI overlay.
            var hudGo = new GameObject("StatusHud");
            hudGo.AddComponent<StatusHud>();

            // Player (the caster). No model needed — this demo shows the simulation,
            // not the rendering. Configure before Start so defaults do not win.
            var playerGo = new GameObject("Player");
            var playerStats = playerGo.AddComponent<CharacterStats>();
            playerStats.Configure(displayName: "Player", maxHealth: 100f, baseDamage: 15f);

            // Enemy (the target).
            var enemyGo = new GameObject("Enemy");
            var enemyStats = enemyGo.AddComponent<CharacterStats>();
            enemyStats.Configure(displayName: "Enemy", maxHealth: 120f, baseDamage: 10f);

            // Wire the spellcaster to the enemy.
            var caster = playerGo.AddComponent<Spellcaster>();
            caster.SetTarget(enemyStats);

            Debug.Log("Demo ready. F = Fire, W = Water, H = Heal (targets the Enemy).");
        }
    }
}
