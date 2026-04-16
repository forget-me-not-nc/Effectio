using Effectio.Effects;

namespace Effectio.Statuses
{
    public interface IStatus
    {
        string Key { get; }
        string[] Tags { get; }
        float Duration { get; }
        int MaxStacks { get; }
        IEffect[] OnApplyEffects { get; }
        IEffect[] OnTickEffects { get; }
        IEffect[] OnRemoveEffects { get; }
        float TickInterval { get; }
    }
}
