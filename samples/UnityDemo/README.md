# Effectio - Unity demo project

A minimal Unity project showing how to wire Effectio into a scene and drive
real gameplay from it. Ships with an elemental-combat demo: the player casts
Fire or Water on an enemy, and if both land the `Vaporize` reaction triggers
automatically (burst damage + stun).

**Watch for:** the UI overlay, and the Console messages every time a reaction
fires. Everything is driven by a single `EffectioManager.Tick(Time.deltaTime)`.

## Prerequisites

- Unity Editor **2022.3 LTS or newer**. Older LTS (2021.3) also works - edit
  `ProjectSettings/ProjectVersion.txt` to the version you have.
- Unity Hub (easiest way to open the project).

## Open the project

1. Open Unity Hub, click **Add project from disk**.
2. Select the `samples/UnityDemo` folder of this repository.
3. Unity Hub will ask which editor version to open with - pick any 2022.3.x.
4. The first open takes ~30 seconds as Unity generates `Library/`, `Temp/`,
   and the per-asset `.meta` sidecars. All of those live outside git.

### How Effectio gets resolved

`Packages/manifest.json` references the library with a **local file path**:

```json
"com.forget-me-not-nc.effectio": "file:../../Effectio"
```

That points at the `Effectio/` folder at the repo root (two levels up from
this project's root, `samples/UnityDemo/`). Consequences:

- Works offline - no network, no NuGet, no tags.
- Any edit to Effectio source shows up in Unity after a recompile. Great for
  learning / experimenting.
- If you move this demo folder outside the repo, change the path (or swap
  to the git-URL form shown in the main README).

## Run the demo

1. `File > New Scene > Empty`.
2. Create an empty GameObject (right-click in Hierarchy, **Create Empty**).
3. Add the **DemoBootstrap** component to it (Add Component -> search for
   `DemoBootstrap`).
4. Press **Play**.
5. In the Game view:
   - **F** - cast Fire on the enemy (applies `Burning`, -5 HP per second for 5s).
   - **W** - cast Water on the enemy (applies `Wet`).
   - **H** - heal the enemy by 20 HP (useful for resetting).
6. Cast F, then W on the same enemy within the 5-second window. The overlay
   should flash to show `Stunned` and the HP should drop by 40 as the
   `Vaporize` reaction consumes both elemental statuses.

## What each script does

| Script | Role |
|---|---|
| `EffectioWorld.cs`  | Scene singleton. Creates the `EffectioManager`, registers statuses + reactions, calls `Tick` every `Update`. |
| `CharacterStats.cs` | MonoBehaviour that maps a GameObject to an Effectio `IEffectioEntity` with Health+Damage stats; dies when Health hits 0. |
| `Spellcaster.cs`    | Reads F/W/H input and applies effects to its target. |
| `StatusHud.cs`      | IMGUI overlay showing everyone's HP and active statuses. |
| `DemoBootstrap.cs`  | One-component scene builder: creates the four GameObjects above so you do not have to configure anything manually. |

## Why there is no scene file checked in

`.unity` scene files are version-controlled YAML - they work, but they also
carry Unity-generated GUIDs and meta files that would break the moment the
sample runs on a different machine. A single `DemoBootstrap` script builds
the scene procedurally at Play time, so the demo is **reproducible on any
machine without any Unity-authored assets in git**.

## Extending the demo (ideas to learn)

- Register a new reaction in `EffectioWorld.RegisterCatalog` (for example
  `Electrified + Wet -> Overloaded`) and see it fire.
- Add a third keybind in `Spellcaster` that applies a `Periodic` DoT
  directly (`EffectBuilder.Create("poison").Periodic(5f, 1f).AdjustStat("Health", -3f)`).
- Add a `Triggered` effect on the player that heals for 15 HP when their
  Health drops below 30 (`EffectBuilder.Create("last_stand").Triggered(...)
  .WhenStatBelow("Health", 30f)`).
- Replace the IMGUI HUD with a proper uGUI Canvas + TextMeshPro.

## Troubleshooting

- **"The type or namespace name 'Effectio' could not be found"** after open -
  Unity needs one recompile pass. `Assets -> Refresh` or wait a few seconds.
- **Nothing happens when I press F** - make sure the Game view has focus
  (click on it) before pressing keys.
- **Unity Hub says "unknown version"** - edit `ProjectSettings/ProjectVersion.txt`
  to the Unity version you have installed.
