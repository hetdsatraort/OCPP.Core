namespace OCPI.Core.Roaming.Services
{
    public interface IPartnerInvoiceClient
    {
        /// <summary>
        /// Returns the invoice breakdown for the given OCPI session id, or null if it couldn't be
        /// obtained (Management API not configured, unreachable, or the session isn't billable
        /// yet). Callers should treat null as "retry later", not "no fee applies".
        /// </summary>
        Task<PartnerInvoiceResult?> GetOrCreateInvoiceAsync(string ocpiSessionId, CancellationToken ct);
    }
}
