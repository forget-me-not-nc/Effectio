using System;

namespace Effectio.Common.Exceptions
{
    public class EffectioException : Exception
    {
        public string Key { get; }

        public EffectioException(string message) : base(message) { }

        public EffectioException(string message, string key) : base(message)
        {
            Key = key;
        }

        public EffectioException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class StatException : EffectioException
    {
        public StatException(string message) : base(message) { }
        public StatException(string message, string key) : base(message, key) { }
    }

    public class ModifierException : EffectioException
    {
        public ModifierException(string message) : base(message) { }
        public ModifierException(string message, string key) : base(message, key) { }
    }

    public class StatusException : EffectioException
    {
        public StatusException(string message) : base(message) { }
        public StatusException(string message, string key) : base(message, key) { }
    }

    public class EffectException : EffectioException
    {
        public EffectException(string message) : base(message) { }
        public EffectException(string message, string key) : base(message, key) { }
    }

    public class EntityException : EffectioException
    {
        public EntityException(string message) : base(message) { }
        public EntityException(string message, string key) : base(message, key) { }
    }
}
