using System;

namespace Effectio.Common.Logging
{
    public interface IEffectioLogger
    {
        /// <summary>
        /// When <c>false</c>, engine hot paths will skip constructing log messages entirely
        /// (avoiding interpolated-string allocations). <see cref="VoidLogger"/> returns <c>false</c>.
        /// </summary>
        bool IsEnabled { get; }

        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception exception = null);
    }
}

