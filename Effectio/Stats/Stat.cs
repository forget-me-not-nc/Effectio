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
            _modifiers.Add(modifier);
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

        public void Recalculate()
        {
            float value = BaseValue;

            // Phase 1: Additive
            foreach (var mod in _modifiers)
            {
                if (mod.Type == ModifierType.Additive)
                    value += mod.Value;
            }

            // Phase 2: Multiplicative
            foreach (var mod in _modifiers)
            {
                if (mod.Type == ModifierType.Multiplicative)
                    value *= mod.Value;
            }

            // Phase 3: Cap adjustments (modify min/max temporarily — applied to the value)
            float effectiveMin = Min;
            float effectiveMax = Max;
            foreach (var mod in _modifiers)
            {
                if (mod.Type == ModifierType.CapAdjustment)
                {
                    effectiveMax += mod.Value;
                }
            }

            CurrentValue = Clamp(value, effectiveMin, effectiveMax);
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
