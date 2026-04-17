# Effectio - Unity walkable demo

A small top-down playground that shows Effectio driving real, frame-by-frame
gameplay in Unity. You walk a cube around a 30x30 floor and step onto coloured
pads; each pad applies a different effect or status, and three reactions fire
automatically when statuses combine.

![demo gif placeholder](Docs/demo.gif)

## What is in the scene

| Pad | Color | What it does |
|---|---|---|
| Fire | red | Applies `Burning` (5 s, -5 HP/s, **stackable x3**) |
| Water | blue | Applies `Wet` (5 s) |
| Lightning | yellow | Applies `Charged` (5 s) |
| Haste | cyan | Applies `Hasted` (+50 % Speed for 5 s) |
| Slow | dark blue | Applies `Slowed` (-50 % Speed for 5 s) |
| Heal | green | Instant +20 HP |
| Sanctuary | white | Cleanses every elemental + debuff |
| Poison | purple | **Three things at once:** periodic -2 HP/s for 8 s + `Slowed` + `Weakened` |
| Bleed | dark red | Applies `Bleeding` (-1 HP/s, 8 s, **stackable x5**) |
| Adrenaline | orange | Arms a `Triggered` effect: when Health drops below 30, instantly +25 HP |

| Reaction | Trigger | Effect |
|---|---|---|
| Vaporize | `Burning` + `Wet` | -40 HP, `Stunned` 2 s |
| Electrocuted | `Wet` + `Charged` | -30 HP |
| Overload | `Burning` + `Charged` | -25 HP, `Powered` (+50 % Damage for 5 s) |
| Frostbite | `Wet` + `Slowed` | -20 HP |
| Apocalypse | `Burning` + `Wet` + `Charged` + `Bleeding` + `Weakened` | **-100 HP**, `Stunned` 3 s |

A red **Enemy** cube spawns at the right side of the floor, walks toward the
player at Speed 3, and applies `Burning` on contact every second.

The Player has `Health.Min = 1`, so it never dies - you can experiment freely.
The Enemy can die normally; lure it onto Poison + Bleed to test that.

## Prerequisites

- Unity Editor **2022.3 LTS** (older 2021.3 also works - edit
  `ProjectSettings/ProjectVersion.txt` to your installed version).
- Unity Hub to manage the editor install.
- Active Input Handling: **Input Manager (Old)** or **Both**. Project Settings
  -> Player -> Active Input Handling. The new Input System on its own will
  not receive `Input.GetAxisRaw` calls.

## Open the project

1. Open Unity Hub, click **Add project from disk**.
2. Select the `samples/UnityDemo` folder of this repository.
3. Pick a 2022.3.x editor when Unity Hub asks.
4. First import takes ~30 s as Unity generates `Library/`, `Temp/`, and
   per-asset `.meta` sidecars - all git-ignored.

### How Effectio gets resolved

`Packages/manifest.json` references the library through a local file path:

```json
"com.forget-me-not-nc.effectio": "file:../../../Effectio"
```

Unity resolves `file:` paths **relative to the location of `manifest.json`
itself** (the `Packages/` folder), so each `..` walks one directory up:
`Packages` -> `samples/UnityDemo` -> `samples` -> repo root, then into
`Effectio`. Consequences:

- Works offline - no network, no NuGet, no tags.
- Edits to `Effectio/**/*.cs` show up in Unity after one recompile. Great
  for experimenting on both sides of the boundary.
- If you copy this folder out of the repo, change the path or swap to the
  git-URL form shown in the main README.

## Run the demo

1. `File -> New Scene -> Empty (Built-in)`. Save it anywhere under `Assets/`.
2. Right-click in **Hierarchy** -> **Create Empty**.
3. With the empty GameObject selected, **Add Component** -> search for
   `DemoBootstrap`, add it.
4. Press **Play**.
5. Use **WASD** or arrow keys to walk the white cube onto the coloured pads.
6. Watch the HUD: top-left shows live HP and statuses, bottom-left shows the
   colour legend. Console logs every reaction that fires.

### Things to try

- **Vaporize**: walk onto Fire, then walk onto Water within 5 s -> -40 HP,
  see `Stunned` appear in the HUD for 2 s.
- **Electrocuted**: Water then Lightning -> -30 HP.
- **Overload**: Fire then Lightning -> -25 HP, then check the Player's
  `Damage` stat in the inspector: it shows 22.5 (= 15 x 1.5) for the next
  5 s.
- **Cleanse**: stack Burning + Wet but step on Sanctuary before they react -
  both statuses vanish, no Vaporize.
- **Survive Poison**: walk onto Poison, then onto Heal repeatedly to outpace
  the DoT.

## What each script does

| Script | Role |
|---|---|
| `EffectioWorld.cs`   | Scene singleton. Owns the `EffectioManager`, registers all statuses (`Burning`, `Wet`, `Charged`, `Stunned`, `Powered`, `Hasted`, `Slowed`, `Weakened`, `Bleeding`) and reactions (`Apocalypse`, `Vaporize`, `Electrocuted`, `Overload`, `Frostbite`), calls `Tick` every `Update`. |
| `CharacterStats.cs`  | MonoBehaviour binding a GameObject to an Effectio `IEffectioEntity` with `Health`, `Damage`, `Speed` stats. `Configure(..., minHealth, baseSpeed)` lets the demo cap player HP at 1 so it never dies. |
| `PlayerMovement.cs`  | Top-down WASD / arrow input on a kinematic Rigidbody. Reads the `Speed` stat each tick, so `Hasted` / `Slowed` modifiers change movement speed in real time. |
| `EffectZone.cs`      | Trigger pad. `OnTriggerEnter` applies the right effect / status / triggered-effect through Effectio. |
| `EnemyAI.cs`         | Wandering enemy. Walks toward the player at its own `Speed` stat; on contact applies `Burning` (1 s cooldown). Has its own `CharacterStats` so the player can lure it onto Poison / Bleed pads to kill it. |
| `StatusHud.cs`       | IMGUI overlay (per-character HP + active statuses with stack counts + colour legend). No Canvas, no UI prefabs. |
| `DemoBootstrap.cs`   | One-component scene builder. Spawns Camera, Light, Floor, ten Pads, Player, Enemy, HUD, and the `EffectioWorld`. The whole scene is procedural so the demo runs identically on any machine. |

## GIF

`samples/UnityDemo/docs/demo.gif`.

1. **Tools**:
   - **ScreenToGif** (Windows, free, https://www.screentogif.com/) -
     easiest workflow: window-target the Game view, record, edit, export.
   - **OBS Studio** for a high-quality .mp4, then convert with `ffmpeg`:
     `ffmpeg -i recording.mp4 -vf "fps=15,scale=720:-1:flags=lanczos" -loop 0 demo.gif`
2. **Recipe** (~12 s loop):
   - Walk to the **Fire** pad (HUD: `Burning` appears).
   - Walk to the **Water** pad. Vaporize fires, HP drops to 60, `Stunned`
     shows.
   - Walk to **Lightning** then **Water**. Electrocuted fires, -30 HP.
   - Walk to **Heal** twice to recover.
   - Walk to **Sanctuary** to show statuses being cleansed.
3. **Target**: ~720p, ~15 fps, &lt; 4 MB so GitHub renders inline.
4. Drop the result at `samples/UnityDemo/docs/demo.gif`. The README image
   reference will resolve automatically.

