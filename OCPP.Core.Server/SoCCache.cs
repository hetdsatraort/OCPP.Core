using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
        /// Called when a new transaction starts: drop a cached value that belongs to a different (previous)
        /// transaction so it can't be picked up as the new session's start SoC.
        /// Values without a transaction (pre-start MeterValues, TransactionId 0/null) are kept.
        /// </summary>
        public static void ClearIfOtherTransaction(string chargePointId, int connectorId, int newTransactionId)
        {
            string key = GetKey(chargePointId, connectorId);
            if (_cache.TryGetValue(key, out var data) &&
                data.TransactionId.HasValue && data.TransactionId.Value > 0 &&
                data.TransactionId.Value != newTransactionId)
            {
                // Only remove the exact entry we inspected (a newer MeterValue may have replaced it meanwhile)
                ((ICollection<KeyValuePair<string, SoCData>>)_cache).Remove(new KeyValuePair<string, SoCData>(key, data));
            }
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
