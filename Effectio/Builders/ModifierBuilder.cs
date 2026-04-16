using System;
using Effectio.Modifiers;

namespace Effectio.Builders
{
    /// <summary>
    /// Fluent builder for the built-in modifier kinds. Call one of
    /// <see cref="Additive"/>, <see cref="Multiplicative"/>, or <see cref="CapAdjustment"/>
    /// before <see cref="Build"/>.
    /// </summary>
    public class ModifierBuilder
    {
        private enum Kind { None, Additive, Multiplicative, CapAdjustment }

        private readonly string _key;
        private Kind _kind = Kind.None;
        private float _value;
        private float _duration = -1f;
        private string _sourceKey;

        public ModifierBuilder(string key)
        {
            _key = key;
        }

        public static ModifierBuilder Create(string key) => new ModifierBuilder(key);

        public ModifierBuilder Additive(float value)       { _kind = Kind.Additive;       _value = value; return this; }
        public ModifierBuilder Multiplicative(float value) { _kind = Kind.Multiplicative; _value = value; return this; }
        public ModifierBuilder CapAdjustment(float value)  { _kind = Kind.CapAdjustment;  _value = value; return this; }

        public ModifierBuilder WithDuration(float duration) { _duration = duration; return this; }
        public ModifierBuilder Permanent() { _duration = -1f; return this; }
        public ModifierBuilder FromSource(string sourceKey) { _sourceKey = sourceKey; return this; }

        public IModifier Build()
        {
            switch (_kind)
            {
                case Kind.Additive:       return new AdditiveModifier(_key, _value, _duration, _sourceKey);
                case Kind.Multiplicative: return new MultiplicativeModifier(_key, _value, _duration, _sourceKey);
                case Kind.CapAdjustment:  return new CapAdjustmentModifier(_key, _value, _duration, _sourceKey);
                default:
                    throw new InvalidOperationException(
                        "ModifierBuilder requires a kind — call Additive / Multiplicative / CapAdjustment before Build.");
            }
        }
    }
}
