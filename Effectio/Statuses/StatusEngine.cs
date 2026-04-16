using System;
using System.Collections.Generic;
using Effectio.Common.Logging;
using Effectio.Entities;

namespace Effectio.Statuses
{
    public class StatusEngine : IStatusEngine
    {
        private readonly IEffectioLogger _logger;

        // Registered status definitions
        private readonly Dictionary<string, IStatus> _statusDefinitions = new Dictionary<string, IStatus>();

        // Active statuses per entity: entityId -> statusKey -> instance data
        private readonly Dictionary<string, Dictionary<string, ActiveStatusData>> _activeStatuses =
            new Dictionary<string, Dictionary<string, ActiveStatusData>>();

        public event Action<IEffectioEntity, string> OnStatusApplied;
        public event Action<IEffectioEntity, string> OnStatusRemoved;
        public event Action<IEffectioEntity, string> OnStatusExpired;

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

        public void ApplyStatus(IEffectioEntity entity, string statusKey)
        {
            if (!_statusDefinitions.TryGetValue(statusKey, out var definition))
            {
                _logger.Warning($"Status '{statusKey}' is not registered. Applying as a simple tag.");
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
                }
                // Refresh duration
                existing.RemainingDuration = definition.Duration;
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

            _logger.Info($"Status '{statusKey}' applied to entity '{entity.Id}'.");
            OnStatusApplied?.Invoke(entity, statusKey);
        }

        public void RemoveStatus(IEffectioEntity entity, string statusKey)
        {
            entity.RemoveStatus(statusKey);

            if (_activeStatuses.TryGetValue(entity.Id, out var entityStatuses))
            {
                entityStatuses.Remove(statusKey);
            }

            _logger.Info($"Status '{statusKey}' removed from entity '{entity.Id}'.");
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

        public void Tick(float deltaTime)
        {
            var expiredList = new List<(string entityId, string statusKey)>();

            foreach (var kvp in _activeStatuses)
            {
                var entityId = kvp.Key;
                foreach (var statusKvp in kvp.Value)
                {
                    var data = statusKvp.Value;
                    var definition = _statusDefinitions.ContainsKey(data.StatusKey)
                        ? _statusDefinitions[data.StatusKey]
                        : null;

                    // Decrement duration (skip permanent statuses)
                    if (data.RemainingDuration >= 0)
                    {
                        data.RemainingDuration -= deltaTime;
                        if (data.RemainingDuration <= 0)
                        {
                            expiredList.Add((entityId, data.StatusKey));
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

            // Process expirations (entity references resolved by manager)
            foreach (var (entityId, statusKey) in expiredList)
            {
                if (_activeStatuses.TryGetValue(entityId, out var entityStatuses))
                {
                    entityStatuses.Remove(statusKey);
                }
            }

            // Store expired for manager to process with entity references
            PendingExpirations = expiredList;
        }

        /// <summary>
        /// Expirations from the last Tick call, to be processed by EffectioManager which has entity references.
        /// </summary>
        internal List<(string entityId, string statusKey)> PendingExpirations { get; private set; }
            = new List<(string, string)>();

        /// <summary>
        /// Get pending tick data for a specific entity.
        /// </summary>
        internal List<string> GetPendingTicks(string entityId)
        {
            var result = new List<string>();
            if (!_activeStatuses.TryGetValue(entityId, out var entityStatuses))
                return result;

            foreach (var kvp in entityStatuses)
            {
                if (kvp.Value.PendingTick)
                {
                    kvp.Value.PendingTick = false;
                    result.Add(kvp.Key);
                }
            }
            return result;
        }

        internal void RaiseStatusExpired(IEffectioEntity entity, string statusKey)
        {
            OnStatusExpired?.Invoke(entity, statusKey);
        }

        internal void CleanupEntity(string entityId)
        {
            _activeStatuses.Remove(entityId);
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
