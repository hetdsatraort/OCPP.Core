using System.Collections.Concurrent;
using System.Text.Json;
using OCPP.Core.Database.OCPIDTO;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Caches each partner's OCPI module endpoint map (GET /versions → GET /versions/2.2.1) for
    /// <c>OCPI:EndpointCacheMinutes</c>.
    ///
    /// Before this existed, every single push (one session PUT, one CDR POST, one EVSE PATCH
    /// notification, one STOP_SESSION command — each potentially its own call) re-ran both
    /// discovery requests first. On <see cref="OcpiOrphanSessionService"/>'s ~10s cycle that meant
    /// 2 extra HTTP calls per partner ahead of every real push, which was a large share of the
    /// request volume tripping the partner's 429 rate limit. Endpoint URLs don't change between
    /// a partner's credential updates, so short-TTL caching is safe and is what the OCPI spec
    /// assumes callers do rather than re-discovering on every call.
    /// </summary>
    public interface IOcpiEndpointCache
    {
        /// <summary>Returns the partner's endpoint map ("{identifier}_{role}" → url, both lower-case,
        /// role empty-string when the partner omits it), using the cache when still fresh.</summary>
        Task<Dictionary<string, string>?> GetEndpointsAsync(
            OcpiPartnerCredential partner, HttpClient http, ILogger logger, CancellationToken ct);

        /// <summary>Looks up one module/role combination, discovering/caching as needed.
        /// Tries each acceptable role in order, then falls back to a role-less entry.</summary>
        Task<string?> ResolveEndpointAsync(
            OcpiPartnerCredential partner, string moduleIdentifier, string[] acceptableRoles,
            HttpClient http, ILogger logger, CancellationToken ct);

        void Invalidate(int partnerId);
    }

    public sealed class OcpiEndpointCache : IOcpiEndpointCache
    {
        private sealed record CacheEntry(Dictionary<string, string> Endpoints, DateTime ExpiresUtc);

        private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();
        private readonly TimeSpan _ttl;

        public OcpiEndpointCache(IConfiguration configuration)
        {
            var minutes = configuration.GetValue<int>("OCPI:EndpointCacheMinutes", 15);
            _ttl = TimeSpan.FromMinutes(Math.Max(1, minutes));
        }

        public void Invalidate(int partnerId) => _cache.TryRemove(partnerId, out _);

        public async Task<Dictionary<string, string>?> GetEndpointsAsync(
            OcpiPartnerCredential partner, HttpClient http, ILogger logger, CancellationToken ct)
        {
            if (_cache.TryGetValue(partner.Id, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
                return cached.Endpoints;

            var endpoints = await DiscoverAsync(partner, http, logger, ct);
            if (endpoints != null)
                _cache[partner.Id] = new CacheEntry(endpoints, DateTime.UtcNow + _ttl);

            return endpoints;
        }

        public async Task<string?> ResolveEndpointAsync(
            OcpiPartnerCredential partner, string moduleIdentifier, string[] acceptableRoles,
            HttpClient http, ILogger logger, CancellationToken ct)
        {
            var endpoints = await GetEndpointsAsync(partner, http, logger, ct);
            if (endpoints == null) return null;

            var id = moduleIdentifier.ToLowerInvariant();
            foreach (var role in acceptableRoles)
                if (endpoints.TryGetValue($"{id}_{role.ToLowerInvariant()}", out var url))
                    return url;

            // Some partner implementations omit "role" on an endpoint entry entirely.
            return endpoints.TryGetValue($"{id}_", out var noRoleUrl) ? noRoleUrl : null;
        }

        private static async Task<Dictionary<string, string>?> DiscoverAsync(
            OcpiPartnerCredential partner, HttpClient http, ILogger logger, CancellationToken ct)
        {
            try
            {
                var partnerUrl = partner.Url.TrimEnd('/').EndsWith("versions")
                    ? partner.Url.TrimEnd('/')
                    : $"{partner.Url.TrimEnd('/')}/versions";

                var vResp = await http.GetAsync(partnerUrl, ct);
                if (!vResp.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "OcpiEndpointCache: partner {CC}-{Party} versions endpoint returned {Status}",
                        partner.CountryCode, partner.PartyId, vResp.StatusCode);
                    return null;
                }

                using var vDoc = JsonDocument.Parse(await vResp.Content.ReadAsStringAsync(ct));
                string? v221Url = null;
                if (vDoc.RootElement.TryGetProperty("data", out var vData) &&
                    vData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in vData.EnumerateArray())
                    {
                        var ver = v.TryGetProperty("version", out var vp) ? vp.GetString() : null;
                        var url = v.TryGetProperty("url", out var up) ? up.GetString() : null;
                        if (ver == "2.2.1") { v221Url = url; break; }
                        if (ver == "2.2") v221Url = url;
                    }
                }

                if (v221Url == null)
                {
                    logger.LogWarning(
                        "OcpiEndpointCache: partner {CC}-{Party} does not advertise OCPI 2.2.1/2.2",
                        partner.CountryCode, partner.PartyId);
                    return null;
                }

                var dResp = await http.GetAsync(v221Url, ct);
                if (!dResp.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "OcpiEndpointCache: partner {CC}-{Party} version details endpoint returned {Status}",
                        partner.CountryCode, partner.PartyId, dResp.StatusCode);
                    return null;
                }

                using var dDoc = JsonDocument.Parse(await dResp.Content.ReadAsStringAsync(ct));
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (dDoc.RootElement.TryGetProperty("data", out var dData) &&
                    dData.TryGetProperty("endpoints", out var eps) &&
                    eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var id = ep.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                        var role = ep.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;
                        var url = ep.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;

                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url)) continue;

                        map[$"{id.ToLowerInvariant()}_{(role ?? string.Empty).ToLowerInvariant()}"] = url;
                    }
                }

                if (map.Count == 0)
                {
                    logger.LogWarning(
                        "OcpiEndpointCache: partner {CC}-{Party} returned empty endpoint list",
                        partner.CountryCode, partner.PartyId);
                    return null;
                }

                logger.LogDebug(
                    "OcpiEndpointCache: discovered/cached {Count} endpoints for partner {CC}-{Party}",
                    map.Count, partner.CountryCode, partner.PartyId);

                return map;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "OcpiEndpointCache: endpoint discovery failed for partner {CC}-{Party}",
                    partner.CountryCode, partner.PartyId);
                return null;
            }
        }
    }
}
