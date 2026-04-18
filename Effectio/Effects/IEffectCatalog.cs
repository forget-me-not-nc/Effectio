using System.Collections.Generic;

namespace Effectio.Effects
{
    /// <summary>
    /// Read/write registry of <see cref="IEffect"/> instances keyed by <see cref="IEffect.Key"/>.
    /// Reactions reference effects by key (e.g. <c>ReactionBuilder.ApplyEffect("DamageBurst")</c>);
    /// the engine resolves the key through this catalog at trigger time.
    /// </summary>
    /// <remarks>
    /// Kept as a separate interface from <see cref="IEffectsEngine"/> to preserve binary
    /// compatibility for v1.0 consumers that may have implemented <see cref="IEffectsEngine"/>
    /// directly. The built-in <c>EffectsEngine</c> implements both.
    /// </remarks>
    public interface IEffectCatalog
    {
        /// <summary>
        /// Registers an effect under its <see cref="IEffect.Key"/>. If the key is already
        /// registered, the previous entry is replaced (matches <c>StatusEngine.RegisterStatus</c>).
        /// </summary>
        void RegisterEffect(IEffect effect);

        /// <summary>
        /// Looks up a previously registered effect by key.
        /// </summary>
        /// <returns><see langword="true"/> if an effect with that key is registered.</returns>
        bool TryGetEffect(string key, out IEffect effect);

        /// <summary>All currently registered effects.</summary>
        IReadOnlyCollection<IEffect> RegisteredEffects { get; }
    }
}
