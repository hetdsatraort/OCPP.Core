using System;

namespace OCPP.Core.Management.Models.UnifiedCharging
{
    public enum ProviderType
    {
        Local,
        Partner
    }

    /// <summary>
    /// Encodes/decodes composite identifiers that let a single field address either a local
    /// (OCPP) entity — keyed by GUID RecId — or an OCPI partner entity — keyed by numeric DB id
    /// (locations/EVSEs/connectors) or by the partner CPO's session id (sessions).
    /// Format: "L:{recId}" for local, "P:{nativeId}" for partner.
    /// </summary>
    public static class UnifiedId
    {
        private const string LocalPrefix = "L:";
        private const string PartnerPrefix = "P:";

        public static string Encode(ProviderType provider, string nativeId)
        {
            if (string.IsNullOrEmpty(nativeId))
                throw new ArgumentException("nativeId must not be empty", nameof(nativeId));

            return (provider == ProviderType.Local ? LocalPrefix : PartnerPrefix) + nativeId;
        }

        public static bool TryParse(string composite, out ProviderType provider, out string nativeId)
        {
            provider = default;
            nativeId = null;

            if (string.IsNullOrEmpty(composite))
                return false;

            if (composite.StartsWith(LocalPrefix, StringComparison.Ordinal))
            {
                provider = ProviderType.Local;
                nativeId = composite.Substring(LocalPrefix.Length);
                return !string.IsNullOrEmpty(nativeId);
            }

            if (composite.StartsWith(PartnerPrefix, StringComparison.Ordinal))
            {
                provider = ProviderType.Partner;
                nativeId = composite.Substring(PartnerPrefix.Length);
                return !string.IsNullOrEmpty(nativeId);
            }

            return false;
        }
    }
}
