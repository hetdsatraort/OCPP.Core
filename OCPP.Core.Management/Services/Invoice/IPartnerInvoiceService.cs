using System.Threading.Tasks;
using OCPP.Core.Database.OCPIDTO;

namespace OCPP.Core.Management.Services.Invoice
{
    public interface IPartnerInvoiceService
    {
        /// <summary>
        /// Returns the existing platform-fee invoice for an OCPI partner session (looked up by
        /// its OCPI session id), or creates one if none exists yet. Throws if the session hasn't
        /// been billed yet (no reported cost) or doesn't exist.
        /// </summary>
        Task<OcpiPartnerSessionInvoice> GetOrCreateInvoiceAsync(string ocpiSessionId);

        /// <summary>
        /// Returns the existing platform-fee invoice for an already-loaded partner session, or
        /// creates one. Used by callers (e.g. OcpiPartnerHubController) that already have the
        /// <see cref="OcpiPartnerSession"/> entity in hand.
        /// </summary>
        Task<OcpiPartnerSessionInvoice> GetOrCreateInvoiceAsync(OcpiPartnerSession session);

        byte[] RenderPdf(OcpiPartnerSessionInvoice invoice);
    }
}
