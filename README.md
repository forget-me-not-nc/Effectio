# Effectio

A Unity-compatible (`netstandard2.0`) C# library for managing game entity **stats**, **modifiers**, **effects**, **statuses**, and cross-status **reactions** — the building blocks behind mechanics like damage-over-time, auras, elemental interactions (e.g. *fire + water = vaporize*), and conditional buffs.

- **Lightweight** — no runtime reflection, no allocations in the tick hot path beyond what modifiers/effects themselves require.
- **Deterministic** — single `Tick(deltaTime)` entry point drives the whole simulation.
- **Extensible** — every subsystem (stats, effects, statuses, reactions) is accessible via its own engine.
- **Unity friendly** — targets `netstandard2.0`, no external dependencies.

---

## Core concepts

| Concept       | Responsibility                                                                 |
|---------------|---------------------------------------------------------------------------------|
| `IStat`       | A named numeric value with `BaseValue`, `Min`/`Max`, and a list of modifiers.   |
| `IModifier`   | Additive / Multiplicative / CapAdjustment change to a stat, optionally timed.   |
| `IEffect`     | Instant, Timed, Periodic, Aura, or Triggered action on a stat or status.        |
| `IStatus`     | A tagged, possibly-stacking condition with on-apply / on-tick / on-remove effects. |
| `IReaction`   | Fires when the required statuses/tags coexist; mutates stats or statuses.       |
| `EffectioManager` | Facade that owns all engines and drives them via a single `Tick(dt)`.       |

## Installation

Add a project reference to `Effectio.csproj`, or drop the compiled `Effectio.dll` into a Unity `Plugins/` folder.

## Quick start

```csharp
using Effectio.Core;
using Effectio.Stats;
using Effectio.Builders;

var manager = new EffectioManager();

var player = manager.CreateEntity("player1");
player.AddStat(new Stat("Health", baseValue: 100f, min: 0f, max: 100f));
player.AddStat(new Stat("Damage", baseValue: 25f));

// Instant heal
var heal = EffectBuilder.Create("Heal").Instant().AdjustStat("Health", 20f).Build();
manager.Effects.ApplyEffect(player, heal);

// Game loop
manager.Tick(deltaTime: 0.016f);
```

## Stats & modifiers

Stats apply their modifiers in a single priority-ordered pass, then clamp to `[Min, effectiveMax]`. The built-in priority bands are `Additive` (100) → `Multiplicative` (200) → `CapAdjustment` (300); custom modifiers can choose any `int` priority.

```csharp
using Effectio.Modifiers;
using Effectio.Builders;

var dmg = player.GetStat("Damage");

// +10 flat for 5 seconds, sourced from "SpellBuff"
dmg.AddModifier(new AdditiveModifier("spell_bonus", 10f, duration: 5f, sourceKey: "SpellBuff"));

// +20% while it lasts, via the fluent builder
dmg.AddModifier(ModifierBuilder.Create("rage")
    .Multiplicative(1.2f)
    .WithDuration(5f)
    .FromSource("SpellBuff")
    .Build());

// Remove everything from a given source
dmg.RemoveModifiersFromSource("SpellBuff");
```

Defining a custom modifier kind is just a new subclass of `ModifierBase`:

```csharp
public sealed class OverrideModifier : ModifierBase
{
    private readonly float _value;
    public override int Priority => ModifierPriority.Override; // 50 — runs before Additive
    public OverrideModifier(string key, float value, float duration = -1f, string sourceKey = null)
        : base(key, duration, sourceKey) => _value = value;
    public override void Apply(ref StatCalculationContext ctx) => ctx.Value = _value;
}
```

Modifier durations are ticked automatically by `EffectioManager.Tick(dt)` — expired modifiers are removed and the stat recalculates.

## Effects

Five effect types, each producible via the fluent `EffectBuilder`:

```csharp
// Timed stat adjustment that expires after 3s
EffectBuilder.Create("Shout").Timed(3f).AdjustStat("Damage", 5f).Build();

// DOT: -2 HP every second for 5 seconds
EffectBuilder.Create("Poison").Periodic(duration: 5f, tickInterval: 1f).AdjustStat("Health", -2f).Build();

// Aura: +30 Armor while active, automatically reverted on removal or expiration
EffectBuilder.Create("GuardAura").Aura(duration: 10f).AdjustStat("Armor", 30f).Build();

// Triggered: apply "Berserk" the first time Health drops below 30
EffectBuilder.Create("LastStand")
    .Triggered(duration: 60f)
    .ApplyStatus("Berserk")
    .WhenStatBelow("Health", 30f)
    .Build();
```

### Custom actions

The built-in action kinds (`AdjustStat`, `ApplyModifier`, `ApplyStatus`, …) cover the common cases. `ApplyModifier` accepts any `IModifier` kind via a factory:

```csharp
// +100% damage as a multiplicative modifier for 5s
EffectBuilder.Create("Rage")
    .Timed(5f)
    .ApplyModifier("Damage", e => new MultiplicativeModifier(e.Key + "_mod", 2f, e.Duration, e.Key))
    .Build();
```

For bespoke gameplay effects, implement `IEffectAction` and hand it to the builder:

```csharp
public sealed class LifestealAction : IEffectAction
{
    private readonly string _hp;
    private readonly float _percent;
    public LifestealAction(string healthStat, float percent) { _hp = healthStat; _percent = percent; }

    public void Execute(in EffectActionContext ctx)
    {
        var hp = ctx.Entity.GetStat(_hp);
        hp.BaseValue += hp.CurrentValue * _percent;
        hp.Recalculate();
    }

    public void Undo(in EffectActionContext ctx) { /* not applicable */ }
}

var lifesteal = EffectBuilder.Create("Lifesteal")
    .Timed(5f)
    .WithAction(new LifestealAction("Health", 0.05f))
    .Build();
```

Aura / Timed effects call `Undo` automatically on expiration or manual removal.

## Statuses

Statuses are tagged conditions with optional duration, stacking, and lifecycle effects:

```csharp
var burning = StatusBuilder.Create("Burning")
    .WithTags("Fire", "Elemental")
    .WithDuration(5f)
    .Stackable(3)
    .WithTickInterval(1f)
    .OnTick(EffectBuilder.Create("burn_tick").Instant().AdjustStat("Health", -3f))
    .OnRemove(EffectBuilder.Create("burn_off").Instant().RemoveStatus("Burning"))
    .Build();

manager.Statuses.RegisterStatus(burning);
manager.Statuses.ApplyStatus(player, "Burning");
```

### Immunities

```csharp
manager.Statuses.GrantImmunity(player, "Poison");
manager.Statuses.OnStatusBlocked += (e, key) => Console.WriteLine($"{e.Id} immune to {key}");

manager.Statuses.ApplyStatus(player, "Poison"); // silently blocked, fires OnStatusBlocked
manager.Statuses.RevokeImmunity(player, "Poison");
```

## Reactions

Reactions fire when all required statuses/tags are present on an entity:

```csharp
var vaporize = ReactionBuilder.Create("Vaporize")
    .RequireStatuses("Burning", "Wet")
    .ConsumesStatuses()              // both statuses get removed
    .AdjustStat("Health", -50f)      // burst damage
    .ApplyStatus("Stunned")
    .Build();

manager.Reactions.RegisterReaction(vaporize);

// Applying the second status triggers the reaction automatically:
manager.Statuses.ApplyStatus(player, "Burning");
manager.Statuses.ApplyStatus(player, "Wet"); // -> Vaporize fires
```

## The game loop

Call `EffectioManager.Tick(deltaTime)` once per frame / simulation step. It:

1. Decrements effect durations and marks periodic / triggered work.
2. Decrements status durations and marks status tick effects.
3. Ticks every stat's modifiers (removing expired ones, recalculating).
4. Executes pending periodic effect ticks, trigger checks, and aura undos.
5. Fires `OnStatusExpired` for expired statuses and runs their `OnRemove` effects.

```csharp
void Update()
{
    manager.Tick(Time.deltaTime);
}
```

## Events

Every engine exposes events for UI / VFX hookup:

```csharp
manager.Effects.OnEffectApplied += (entity, effect) => { /* … */ };
manager.Effects.OnEffectTick    += (entity, effect) => { /* … */ };
manager.Effects.OnEffectRemoved += (entity, effect) => { /* … */ };

manager.Statuses.OnStatusApplied += (entity, key) => { /* … */ };
manager.Statuses.OnStatusExpired += (entity, key) => { /* … */ };
manager.Statuses.OnStatusBlocked += (entity, key) => { /* … */ };

player.GetStat("Health").OnValueChanged += (stat, oldV, newV) => { /* UI update */ };
```

## License

See [LICENSE.txt](LICENSE.txt).
