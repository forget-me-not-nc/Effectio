using System;
using System.Collections.Generic;
using Effectio.Common.Logging;
using Effectio.Entities;

namespace Effectio.Statuses
{
    public class StatusEngine : IStatusEngine, IStackOperations
    {
        private readonly IEffectioLogger _logger;

        // Registered status definitions
        private readonly Dictionary<string, IStatus> _statusDefinitions = new Dictionary<string, IStatus>();

        // Active statuses per entity: entityId -> statusKey -> instance data
        private readonly Dictionary<string, Dictionary<string, ActiveStatusData>> _activeStatuses =
            new Dictionary<string, Dictionary<string, ActiveStatusData>>();

        // Immunities per entity: entityId -> set of immune status keys
        private readonly Dictionary<string, HashSet<string>> _immunities =
            new Dictionary<string, HashSet<string>>();

        public event Action<IEffectioEntity, string> OnStatusApplied;
        public event Action<IEffectioEntity, string> OnStatusRemoved;
        public event Action<IEffectioEntity, string> OnStatusExpired;
        public event Action<IEffectioEntity, string> OnStatusBlocked;

        // v1.1: fires when a status's stack counter changes (existing status
        // application incremented stacks, or RemoveStacks performed a partial
        // decrement). Does NOT fire on first application (use OnStatusApplied)
        // or full removal (use OnStatusRemoved), and does NOT fire when
        // ApplyStatus is called at MaxStacks (no counter change). Exposed via
        // IStackOperations.OnStatusStacked.
        public event Action<IEffectioEntity, string> OnStatusStacked;

        // v1.1: fires every time ApplyStatus is called against an entity that
        // already has the status. RemainingDuration is refreshed first; then
        // this fires. Distinct from OnStatusStacked: OnStatusRefreshed fires for
        // both the stack-increment path AND the at-MaxStacks refresh-only path.
        public event Action<IEffectioEntity, string> OnStatusRefreshed;

        public StatusEngine(IEffectioLogger logger = null)
        {
            _logger = logger ?? VoidLogger.Instance;
        }

        public void RegisterStatus(IStatus status)
        {
            _statusDefinitions[status.Key] = status;
        }

        public IStatus GetStatusDefinition(string statusKey)
        {
            _statusDefinitions.TryGetValue(statusKey, out var status);
            return status;
        }

        public void GrantImmunity(IEffectioEntity entity, string statusKey)
        {
            if (!_immunities.TryGetValue(entity.Id, out var set))
            {
                set = new HashSet<string>();
                _immunities[entity.Id] = set;
            }
            set.Add(statusKey);
            _logger.Info($"Entity '{entity.Id}' granted immunity to '{statusKey}'.");
        }

        public void RevokeImmunity(IEffectioEntity entity, string statusKey)
        {
            if (_immunities.TryGetValue(entity.Id, out var set))
                set.Remove(statusKey);
        }

        public bool IsImmune(IEffectioEntity entity, string statusKey)
        {
            return _immunities.TryGetValue(entity.Id, out var set) && set.Contains(statusKey);
        }

        public void ApplyStatus(IEffectioEntity entity, string statusKey)
        {
            // Check immunity
            if (IsImmune(entity, statusKey))
            {
                if (_logger.IsEnabled) _logger.Info($"Status '{statusKey}' blocked on entity '{entity.Id}' (immune).");
                OnStatusBlocked?.Invoke(entity, statusKey);
                return;
            }

            if (!_statusDefinitions.TryGetValue(statusKey, out var definition))
            {
                if (_logger.IsEnabled) _logger.Warning($"Status '{statusKey}' is not registered. Applying as a simple tag.");
                // Allow applying unregistered statuses as simple tags (no duration/effects)
                entity.AddStatus(statusKey);
                OnStatusApplied?.Invoke(entity, statusKey);
                return;
            }

            if (!_activeStatuses.TryGetValue(entity.Id, out var entityStatuses))
            {
                entityStatuses = new Dictionary<string, ActiveStatusData>();
                _activeStatuses[entity.Id] = entityStatuses;
            }

            if (entityStatuses.TryGetValue(statusKey, out var existing))
            {
                // Already has this status — try to stack or refresh
                if (existing.Stacks < definition.MaxStacks)
                {
                    existing.Stacks++;
                    if (_logger.IsEnabled) _logger.Info($"Status '{statusKey}' on entity '{entity.Id}' incremented to {existing.Stacks} stacks.");
                    OnStatusStacked?.Invoke(entity, statusKey);
                }
                // Refresh duration
                existing.RemainingDuration = definition.Duration;
                // v1.1: notify refresh listeners (fires for both stack-increment and at-max paths).
                OnStatusRefreshed?.Invoke(entity, statusKey);
                return;
            }

            // New status application
            var data = new ActiveStatusData
            {
                StatusKey = statusKey,
                RemainingDuration = definition.Duration,
                Stacks = 1,
                TimeSinceLastTick = 0f
            };
            entityStatuses[statusKey] = data;
            entity.AddStatus(statusKey);

            if (_logger.IsEnabled) _logger.Info($"Status '{statusKey}' applied to entity '{entity.Id}'.");
            OnStatusApplied?.Invoke(entity, statusKey);
        }

        public void RemoveStatus(IEffectioEntity entity, string statusKey)
        {
            entity.RemoveStatus(statusKey);

            if (_activeStatuses.TryGetValue(entity.Id, out var entityStatuses))
            {
                entityStatuses.Remove(statusKey);
            }

            if (_logger.IsEnabled) _logger.Info($"Status '{statusKey}' removed from entity '{entity.Id}'.");
            OnStatusRemoved?.Invoke(entity, statusKey);
        }

        public bool HasStatus(IEffectioEntity entity, string statusKey)
        {
            return entity.HasStatus(statusKey);
        }

        public int GetStacks(IEffectioEntity entity, string statusKey)
        {
            if (_activeStatuses.TryGetValue(entity.Id, out var entityStatuses))
            {
                if (entityStatuses.TryGetValue(statusKey, out var data))
                    return data.Stacks;
            }
            return 0;
        }

        // -------- IStackOperations --------

        public void RemoveStacks(IEffectioEntity entity, string statusKey, int count)
        {
            if (count <= 0) return;
            if (!_activeStatuses.TryGetValue(entity.Id, out var entityStatuses)) return;
            if (!entityStatuses.TryGetValue(statusKey, out var data)) return;

            int newStacks = data.Stacks - count;
            if (newStacks <= 0)
            {
                // Full removal - delegates to RemoveStatus so the entity-side
                // HashSet, the engine-side dict and OnStatusRemoved all stay in sync.
                RemoveStatus(entity, statusKey);
                return;
            }

            data.Stacks = newStacks;
            if (_logger.IsEnabled) _logger.Info($"Status '{statusKey}' on entity '{entity.Id}' decremented to {newStacks} stacks.");
            OnStatusStacked?.Invoke(entity, statusKey);
        }

        public void Tick(float deltaTime)
        {
            // Reuse the pooled buffer so Tick is allocation-free.
            _expiredBuffer.Clear();

            foreach (var kvp in _activeStatuses)
            {
                var entityId = kvp.Key;
                foreach (var statusKvp in kvp.Value)
                {
                    var data = statusKvp.Value;
                    _statusDefinitions.TryGetValue(data.StatusKey, out var definition);

                    // Decrement duration (skip permanent statuses)
                    if (data.RemainingDuration >= 0)
                    {
                        data.RemainingDuration -= deltaTime;
                        if (data.RemainingDuration <= 0)
                        {
                            _expiredBuffer.Add((entityId, data.StatusKey));
                            continue;
                        }
                    }

                    // Track tick timing (actual tick effects processed by manager with entity reference)
                    if (definition != null && definition.OnTickEffects.Length > 0 && definition.TickInterval > 0)
                    {
                        data.TimeSinceLastTick += deltaTime;
                        if (data.TimeSinceLastTick >= definition.TickInterval)
                        {
                            data.TimeSinceLastTick -= definition.TickInterval;
                            data.PendingTick = true;
                        }
                    }
                }
            }

            // Remove expired entries from the active map (entity-level cleanup happens in manager).
            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                var (entityId, statusKey) = _expiredBuffer[i];
                if (_activeStatuses.TryGetValue(entityId, out var entityStatuses))
                    entityStatuses.Remove(statusKey);
            }
        }

        /// <summary>
        /// Expirations from the last Tick call, to be processed by EffectioManager which has entity references.
        /// Manager is expected to call <c>Clear()</c> after processing — the buffer is then reused on the next Tick.
        /// </summary>
        internal List<(string entityId, string statusKey)> PendingExpirations => _expiredBuffer;

        private readonly List<(string entityId, string statusKey)> _expiredBuffer
            = new List<(string, string)>();

        /// <summary>
        /// Get pending tick data for a specific entity. The returned list is a reused buffer —
        /// callers must iterate it before the next <c>GetPendingTicks</c> call.
        /// </summary>
        internal List<string> GetPendingTicks(string entityId)
        {
            _pendingTicksBuffer.Clear();
            if (!_activeStatuses.TryGetValue(entityId, out var entityStatuses))
                return _pendingTicksBuffer;

            foreach (var kvp in entityStatuses)
            {
                if (kvp.Value.PendingTick)
                {
                    kvp.Value.PendingTick = false;
                    _pendingTicksBuffer.Add(kvp.Key);
                }
            }
            return _pendingTicksBuffer;
        }

        private readonly List<string> _pendingTicksBuffer = new List<string>();

        internal void RaiseStatusExpired(IEffectioEntity entity, string statusKey)
        {
            OnStatusExpired?.Invoke(entity, statusKey);
        }

        internal void CleanupEntity(string entityId)
        {
            _activeStatuses.Remove(entityId);
            _immunities.Remove(entityId);
        }

        private class ActiveStatusData
        {
            public string StatusKey;
            public float RemainingDuration;
            public int Stacks;
            public float TimeSinceLastTick;
            public bool PendingTick;
        }
    }
}
