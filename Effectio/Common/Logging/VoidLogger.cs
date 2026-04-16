using System;

namespace Effectio.Common.Logging
{
    internal sealed class VoidLogger : IEffectioLogger
    {
        public static readonly VoidLogger Instance = new VoidLogger();

        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception exception = null) { }
    }
}
