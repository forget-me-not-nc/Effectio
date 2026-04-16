using System;

namespace Effectio.Common.Logging
{
    public interface IEffectioLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception exception = null);
    }
}
