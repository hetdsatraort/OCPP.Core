using System;
using System.Collections.Concurrent;

namespace OCPP.Core.Server
{
    public class SoCCache
    {
        private static readonly ConcurrentDictionary<string, SoCData> _cache = new ConcurrentDictionary<string, SoCData>();

        public class SoCData
        {
            public double Value { get; set; }
            public DateTime Timestamp { get; set; }
            public int? TransactionId { get; set; }
        }

        /// <summary>
        /// Store or update SoC value for a connector
        /// </summary>
        public static void Set(string chargePointId, int connectorId, double soc, DateTime timestamp, int? transactionId = null)
        {
            string key = GetKey(chargePointId, connectorId);
            _cache[key] = new SoCData
            {
                Value = soc,
                Timestamp = timestamp,
                TransactionId = transactionId
            };
        }

        /// <summary>
        /// Get cached SoC value for a connector
        /// </summary>
        public static SoCData Get(string chargePointId, int connectorId)
        {
            string key = GetKey(chargePointId, connectorId);
            _cache.TryGetValue(key, out var data);
            return data;
        }

        /// <summary>
        /// Clear SoC cache for a connector
        /// </summary>
        public static void Clear(string chargePointId, int connectorId)
        {
            string key = GetKey(chargePointId, connectorId);
            _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Get SoC value if it's recent (within specified minutes)
        /// </summary>
        public static SoCData GetIfRecent(string chargePointId, int connectorId, int maxAgeMinutes = 5)
        {
            var data = Get(chargePointId, connectorId);
            if (data != null && (DateTime.UtcNow - data.Timestamp).TotalMinutes <= maxAgeMinutes)
            {
                return data;
            }
            return null;
        }

        private static string GetKey(string chargePointId, int connectorId)
        {
            return $"{chargePointId}_{connectorId}";
        }
    }
}
