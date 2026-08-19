using System;
using System.Collections.Generic;
using System.Linq;
using Effectio.Modifiers;

namespace Effectio.Stats
{
    public class Stat : IStat
    {
        private readonly List<IModifier> _modifiers = new List<IModifier>();
        private float _currentValue;

        private float _baseValue;
        private float _min = float.MinValue;
        private float _max = float.MaxValue;

        public string Key { get; }

        /// <summary>
        /// The stat's own value, before modifiers - and always inside the stat's own
        /// <see cref="Min"/> and <see cref="Max"/>.
        ///
        /// Held to those bounds on the way in, which it was not before v1.1. Left free it
        /// becomes an accumulator with no floor: a damage-over-time effect on a health stat
        /// bottomed out at zero kept subtracting, so forty ticks of -5 left the base at -100
        /// while the current value sat honestly at 0. The next heal then paid off that debt
        /// invisibly - the player healed for fifty and saw nothing change. Nothing is wrong
        /// at the moment the bug happens, which is why it only ever showed up after a long
        /// fight, which is exactly when somebody notices.
        ///
        /// Bounded by this stat's own limits and not by the effective ones a modifier
        /// computes: raising a ceiling is what modifiers are for, and a current value above
        /// the base is the ordinary result of one. A temporary floor must not permanently
        /// heal the thing it was protecting.
        /// </summary>
        public float BaseValue
        {
            get => _baseValue;
            set => _baseValue = Clamp(value);
        }

        /// <summary>
        /// The floor. Moving it pulls <see cref="BaseValue"/> along if the base is now
        /// outside - the bounds are the stat's promise about itself, and a promise that only
        /// holds until somebody changes it is not one.
        /// </summary>
        public float Min
        {
            get => _min;
            set
            {
                _min = value;
                _baseValue = Clamp(_baseValue);
            }
        }

        /// <inheritdoc cref="Min"/>
        public float Max
        {
            get => _max;
            set
            {
                _max = value;
                _baseValue = Clamp(_baseValue);
            }
        }

        public float CurrentValue
        {
            get => _currentValue;
            private set
            {
                var oldValue = _currentValue;
                _currentValue = value;
                if (Math.Abs(oldValue - _currentValue) > float.Epsilon)
                {
                    OnValueChanged?.Invoke(this, oldValue, _currentValue);
                }
            }
        }

        public IReadOnlyList<IModifier> Modifiers => _modifiers;

        public event Action<IStat, float, float> OnValueChanged;

        public Stat(string key, float baseValue, float min = float.MinValue, float max = float.MaxValue)
        {
            Key = key;

            // Bounds first. The base is clamped as it is assigned, so setting it against the
            // not-yet-assigned defaults would clamp it against the wrong pair of numbers.
            _min = min;
            _max = max;
            _baseValue = Clamp(baseValue);
            _currentValue = _baseValue;
        }

        public void AddModifier(IModifier modifier)
        {
            // Stable sorted insert by Priority — ensures Recalculate can iterate once.
            int i = 0;
            for (; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Priority > modifier.Priority)
                    break;
            }
            _modifiers.Insert(i, modifier);
            Recalculate();
        }

        public void RemoveModifier(string modifierKey)
        {
            _modifiers.RemoveAll(m => m.Key == modifierKey);
            Recalculate();
        }

        public void RemoveModifiersFromSource(string sourceKey)
        {
            _modifiers.RemoveAll(m => m.SourceKey == sourceKey);
            Recalculate();
        }

        public bool TickModifiers(float deltaTime)
        {
            bool anyExpired = false;
            foreach (var mod in _modifiers)
            {
                if (mod.Duration >= 0)
                    mod.RemainingTime -= deltaTime;
            }

            int removed = _modifiers.RemoveAll(m => m.IsExpired);
            if (removed > 0)
            {
                anyExpired = true;
                Recalculate();
            }
            return anyExpired;
        }

        public void Recalculate()
        {
            var ctx = new StatCalculationContext
            {
                Value = BaseValue,
                EffectiveMin = Min,
                EffectiveMax = Max
            };

            // Single pass — _modifiers is kept priority-sorted in AddModifier.
            foreach (var mod in _modifiers)
            {
                mod.Apply(ref ctx);
            }

            CurrentValue = Clamp(ctx.Value, ctx.EffectiveMin, ctx.EffectiveMax);
        }

        private float Clamp(float value)
        {
            return Clamp(value, Min, Max);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
