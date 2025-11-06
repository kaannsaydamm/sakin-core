using Serilog.Core;
using Serilog.Events;

namespace Sakin.Common.Logging
{
    internal sealed class DefaultLogPropertyEnricher : ILogEventEnricher
    {
        private readonly string _propertyName;
        private readonly object? _defaultValue;

        public DefaultLogPropertyEnricher(string propertyName, object? defaultValue)
        {
            _propertyName = propertyName;
            _defaultValue = defaultValue;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.ContainsKey(_propertyName))
            {
                return;
            }

            var property = propertyFactory.CreateProperty(_propertyName, _defaultValue, destructureObjects: true);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}
