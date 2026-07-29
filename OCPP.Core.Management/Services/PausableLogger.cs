using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Management.Services
{
    /// <summary>
    /// ILogger decorator that no-ops when the configured boolean flag is true. The flag is read
    /// from IConfiguration on every call (not cached) so toggling it in appsettings.json takes
    /// effect immediately via the config provider's reloadOnChange, without an app restart.
    /// </summary>
    public class PausableLogger<T> : ILogger<T>
    {
        private readonly ILogger<T> _inner;
        private readonly IConfiguration _configuration;
        private readonly string _pauseFlagKey;

        public PausableLogger(ILogger<T> inner, IConfiguration configuration, string pauseFlagKey)
        {
            _inner = inner;
            _configuration = configuration;
            _pauseFlagKey = pauseFlagKey;
        }

        private bool IsPaused => _configuration.GetValue<bool>(_pauseFlagKey, false);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel)
            => !IsPaused && _inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsPaused) return;
            _inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
