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

        public string Key { get; }
        public float BaseValue { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }

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
            BaseValue = baseValue;
            Min = min;
            Max = max;
            _currentValue = Clamp(baseValue);
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
