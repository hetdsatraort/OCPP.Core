using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace OCPI.Core.Roaming.Services
{
    /// <summary>
    /// Local implementation of OCPI version discovery — no NuGet service dependency.
    /// Reflects over the assembly at startup to build the endpoint list for each
    /// supported OCPI version, using the [OcpiEndpoint] and [Route] attributes
    /// already present on every OCPI controller.
    /// </summary>
    public class OcpiVersionService : IOcpiVersionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OcpiVersionService> _logger;

        // Lazily resolved once per application lifetime
        private static readonly Lazy<Type?> _ocpiEndpointAttrType = new(
            () => AppDomain.CurrentDomain.GetAssemblies()
                      .SelectMany(SafeGetTypes)
                      .FirstOrDefault(t => t.Name == "OcpiEndpointAttribute"),
            isThreadSafe: true);

        public OcpiVersionService(IConfiguration configuration, ILogger<OcpiVersionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // ── IOcpiVersionService ────────────────────────────────────────────────

        public List<OcpiVersionInfo> GetVersions()
        {
            var baseUrl = GetBaseUrl();
            return new List<OcpiVersionInfo>
            {
                new() { Version = "2.2.1", Url = $"{baseUrl}/versions/2.2.1" }
            };
        }

        public OcpiVersionDetails? GetVersionDetails(string version)
        {
            if (!string.Equals(version, "2.2.1", StringComparison.OrdinalIgnoreCase))
                return null;

            var baseUrl  = GetBaseUrl();
            var endpoints = DiscoverEndpoints(baseUrl);

            _logger.LogDebug("OCPI version discovery found {Count} endpoint entries for version {Version}",
                endpoints.Count, version);

            return new OcpiVersionDetails
            {
                Version   = "2.2.1",
                Endpoints = endpoints
            };
        }

        // ── Endpoint discovery ─────────────────────────────────────────────────

        private List<OcpiEndpointEntry> DiscoverEndpoints(string baseUrl)
        {
            var result = new List<OcpiEndpointEntry>();

            var attrType = _ocpiEndpointAttrType.Value;
            if (attrType == null)
            {
                _logger.LogWarning("OcpiEndpointAttribute type could not be found in loaded assemblies. " +
                                   "Version endpoint list will be empty.");
                return result;
            }

            var moduleProp = attrType.GetProperty("Module");
            var rolesProp  = attrType.GetProperty("Roles");

            foreach (var type in typeof(OcpiVersionService).Assembly.GetTypes())
            {
                var attr = type.GetCustomAttributes(attrType, inherit: false).FirstOrDefault();
                if (attr == null) continue;

                // Route template is required to build the full endpoint URL
                var routeAttr = type.GetCustomAttribute<RouteAttribute>();
                if (routeAttr == null) continue;

                // Module → lowercase identifier  e.g. OcpiModule.CDRs → "cdrs"
                var moduleStr = moduleProp?.GetValue(attr)?.ToString()?.ToLowerInvariant();
                if (string.IsNullOrEmpty(moduleStr)) continue;

                // Roles is IEnumerable<InterfaceRole>; use ToString() to stay namespace-agnostic
                var rolesObj = rolesProp?.GetValue(attr);
                var roles = rolesObj is System.Collections.IEnumerable enumerable
                    ? enumerable.Cast<object>()
                                .Select(r => r.ToString()?.ToUpperInvariant() ?? string.Empty)
                                .Where(r => r.Length > 0)
                                .ToList()
                    : new List<string> { "SENDER" };

                var url = $"{baseUrl}/{routeAttr.Template.TrimStart('/')}";

                foreach (var role in roles)
                {
                    result.Add(new OcpiEndpointEntry
                    {
                        Identifier = moduleStr,
                        Role       = role,
                        Url        = url
                    });
                }
            }

            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetBaseUrl()
            => _configuration.GetValue<string>("OCPI:BaseUrl")?.TrimEnd('/')
               ?? throw new InvalidOperationException("OCPI:BaseUrl is not configured. " +
                   "Add it to appsettings.json under 'OCPI:BaseUrl'.");

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try   { return assembly.GetTypes(); }
            catch { return Type.EmptyTypes; }
        }
    }
}
