using System;
using System.Collections.Generic;
using Effectio.Modifiers;

namespace Effectio.Stats
{
    public interface IStat
    {
        string Key { get; }
        float BaseValue { get; set; }
        float CurrentValue { get; }
        float Min { get; set; }
        float Max { get; set; }
        IReadOnlyList<IModifier> Modifiers { get; }

        void AddModifier(IModifier modifier);
        void RemoveModifier(string modifierKey);
        void RemoveModifiersFromSource(string sourceKey);
        void Recalculate();

        event Action<IStat, float, float> OnValueChanged;
    }
}
