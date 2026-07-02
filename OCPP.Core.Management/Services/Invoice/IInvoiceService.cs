using System.Threading.Tasks;
using OCPP.Core.Database.EVCDTO;

namespace OCPP.Core.Management.Services.Invoice
{
    public interface IInvoiceService
    {
        /// <summary>
        /// Returns the existing invoice for a charging session, or creates one (allocating the
        /// next invoice number for the current financial year) if none exists yet.
        /// </summary>
        Task<SessionInvoice> GetOrCreateInvoiceAsync(string chargingSessionId);

        byte[] RenderPdf(SessionInvoice invoice);
    }
}
