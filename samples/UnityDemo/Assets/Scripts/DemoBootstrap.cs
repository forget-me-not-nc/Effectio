using UnityEngine;

namespace EffectioDemo
{
    /// <summary>
    /// One-component demo bootstrapper. Drop this on a single empty GameObject in a
    /// fresh scene, press Play, and everything else (camera, light, floor, six effect
    /// pads, the player cube, the HUD, and the EffectioWorld singleton) is created
    /// procedurally. Goal: zero scene wiring so the interesting bit is the Effectio
    /// code itself, not Unity setup.
    /// </summary>
    public class DemoBootstrap : MonoBehaviour
    {
        // Pad layout (X, Z) on the floor plane. 5x2 grid centered on origin.
        static readonly (string label, ZoneKind kind, Color color, Vector3 pos)[] _zones =
        {
            ("Fire",       ZoneKind.Fire,       new Color(1.00f, 0.30f, 0.20f), new Vector3(-12f, 0.05f,  6f)),
            ("Water",      ZoneKind.Water,      new Color(0.20f, 0.50f, 1.00f), new Vector3( -6f, 0.05f,  6f)),
            ("Lightning",  ZoneKind.Lightning,  new Color(1.00f, 0.85f, 0.20f), new Vector3(  0f, 0.05f,  6f)),
            ("Haste",      ZoneKind.Haste,      new Color(0.30f, 0.95f, 0.95f), new Vector3(  6f, 0.05f,  6f)),
            ("Slow",       ZoneKind.Slow,       new Color(0.20f, 0.25f, 0.55f), new Vector3( 12f, 0.05f,  6f)),
            ("Heal",       ZoneKind.Heal,       new Color(0.30f, 1.00f, 0.40f), new Vector3(-12f, 0.05f, -6f)),
            ("Sanctuary",  ZoneKind.Sanctuary,  new Color(0.95f, 0.95f, 0.95f), new Vector3( -6f, 0.05f, -6f)),
            ("Poison",     ZoneKind.Poison,     new Color(0.70f, 0.30f, 1.00f), new Vector3(  0f, 0.05f, -6f)),
            ("Bleed",      ZoneKind.Bleed,      new Color(0.55f, 0.10f, 0.10f), new Vector3(  6f, 0.05f, -6f)),
            ("Adrenaline", ZoneKind.Adrenaline, new Color(1.00f, 0.55f, 0.10f), new Vector3( 12f, 0.05f, -6f)),
        };

        void Awake()
        {
            EnsureMainCamera();
            EnsureDirectionalLight();

            // EffectioWorld owns the EffectioManager and drives Tick. Singleton.
            new GameObject("EffectioWorld").AddComponent<EffectioWorld>();

            // HUD: read-only IMGUI overlay (controls + character stats + pad legend).
            new GameObject("StatusHud").AddComponent<StatusHud>();

            BuildFloor();
            BuildZones();
            BuildPlayer();
            BuildEnemy();

            Debug.Log("Demo ready. WASD or arrow keys to walk. Step on coloured pads.");
        }

        // -- Scene plumbing ---------------------------------------------------

        void EnsureMainCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0f, 22f, -12f);
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            cam.orthographic = false;
            cam.fieldOfView = 60f;
        }

        void EnsureDirectionalLight()
        {
            foreach (var l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) return;

            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // -- World content ----------------------------------------------------

        void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            // Plane primitive is 10x10 units by default; scale 4 wide x 2 deep => 40x20.
            floor.transform.localScale = new Vector3(4f, 1f, 2f);
            Tint(floor, new Color(0.18f, 0.19f, 0.22f));
        }

        void BuildZones()
        {
            for (int i = 0; i < _zones.Length; i++)
            {
                var z = _zones[i];

                var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "Pad_" + z.label;
                pad.transform.position = z.pos;
                pad.transform.localScale = new Vector3(2.8f, 0.1f, 2.8f);
                Tint(pad, z.color);

                var box = pad.GetComponent<BoxCollider>();
                box.isTrigger = true;
                // Inflate trigger upward so the player cube enters easily.
                box.size = new Vector3(box.size.x, 4f, box.size.z);
                box.center = new Vector3(0f, 1f, 0f);

                var zone = pad.AddComponent<EffectZone>();
                zone.Configure(z.kind);
            }
        }

        void BuildPlayer()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0.5f, 0f);
            player.transform.localScale = Vector3.one * 0.9f;
            Tint(player, new Color(0.92f, 0.94f, 1.00f));

            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Health min = 1 keeps the demo running; player can soak ticks but never dies.
            var stats = player.AddComponent<CharacterStats>();
            stats.Configure(displayName: "Player", maxHealth: 100f, baseDamage: 15f,
                            minHealth: 1f, baseSpeed: 6f);

            player.AddComponent<PlayerMovement>();
        }

        void BuildEnemy()
        {
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enemy.name = "Enemy";
            enemy.transform.position = new Vector3(10f, 0.5f, 0f);
            enemy.transform.localScale = Vector3.one * 0.9f;
            Tint(enemy, new Color(0.85f, 0.20f, 0.20f));

            // Kinematic so the enemy can move without fighting physics; trigger collider
            // so EnemyAI can detect contact with the player without a bounce.
            var rb = enemy.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var stats = enemy.AddComponent<CharacterStats>();
            stats.Configure(displayName: "Enemy", maxHealth: 60f, baseDamage: 10f,
                            minHealth: 0f, baseSpeed: 3f);

            enemy.AddComponent<EnemyAI>();
        }

        // -- Helpers ----------------------------------------------------------

        /// <summary>
        /// Sets the main / albedo colour of a primitive. Writes both <c>_BaseColor</c>
        /// (URP / HDRP Lit) and <c>_Color</c> (built-in Standard) so the demo looks
        /// right regardless of the active render pipeline.
        /// </summary>
        static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var mat = new Material(renderer.sharedMaterial);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            renderer.material = mat;
        }
    }
}

