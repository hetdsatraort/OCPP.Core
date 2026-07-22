using System;
using OCPP.Core.Database.OCPIDTO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OCPP.Core.Management.Services.Invoice
{
    /// <summary>
    /// Tax invoice PDF for an OCPI roaming session. Adapted from <see cref="InvoiceDocument"/>
    /// (local sessions) — CGST/SGST is charged on the combined taxable value (the partner
    /// network's own energy cost plus HyCharge's platform fee), so the invoice covers the full
    /// amount actually billed to the user's wallet.
    /// </summary>
    public class PartnerInvoiceDocument : IDocument
    {
        private static readonly string BrandColor = Colors.Blue.Medium;
        private static readonly string HeadingColor = Colors.Blue.Darken2;
        private static readonly string MutedColor = Colors.Grey.Darken1;
        private static readonly string PanelColor = Colors.Grey.Lighten4;

        private const string CompanyName = "OneRoof Technologies LLP";
        private const string CompanyGstin = "27AAEFO4244L1Z1";
        private const string CompanyAddress = "503, Ecstasy Business Park, JSD, near City of Joy, Ashok Nagar, Mulund West, Mumbai, Maharashtra 400080";
        private const string CompanyEmail = "inquiry@hycharge.in";

        private const string BankAccountName = "OneRoof Technologies LLP";
        private const string BankName = "ICICI Bank";
        private const string BankAccountNo = "124605003983";
        private const string BankIfsc = "ICIC0001246";
        private const string BankBranch = "Mulund West";

        private readonly OcpiPartnerSessionInvoice _invoice;

        public PartnerInvoiceDocument(OcpiPartnerSessionInvoice invoice)
        {
            _invoice = invoice;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(110).Height(45).Border(1)
                        .BorderColor(Colors.Grey.Lighten1)
                        .AlignMiddle().AlignCenter()
                        .Text("ORT LOGO").FontSize(8).FontColor(Colors.Grey.Medium);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignRight().Text("INVOICE").FontSize(22).Bold().FontColor(HeadingColor);
                        col.Item().AlignRight().Text("Invoice Type: Roaming Session (OCPI)").FontSize(9).FontColor(MutedColor);
                    });
                });

                column.Item().PaddingTop(8).Height(3).Background(BrandColor);
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingTop(12).Column(column =>
            {
                column.Spacing(12);

                column.Item().Element(c => ComposeSectionHeading(c, "INVOICE DETAILS"));
                column.Item().Element(ComposeInvoiceDetails);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(ComposeBilledTo);
                    row.ConstantItem(24);
                    row.RelativeItem().Element(ComposeBilledBy);
                });

                column.Item().Element(c => ComposeSectionHeading(c, "ROAMING SESSION"));
                column.Item().Element(ComposeSessionInfo);

                column.Item().Element(c => ComposeSectionHeading(c, "CHARGES"));
                column.Item().Element(ComposeFeeTable);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(ComposeBankDetails);
                    row.ConstantItem(24);
                    row.RelativeItem().Element(ComposeTotals);
                });

                column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(MutedColor));
                    text.Span("Note: ").Bold();
                    text.Span(
                        "This invoice covers the full cost of this roaming session — the partner network's " +
                        "own energy cost plus HyCharge's platform/facilitation fee — with CGST/SGST applied " +
                        "on the combined amount. Amounts are in INR.");
                });
            });
        }

        private void ComposeSectionHeading(IContainer container, string title)
        {
            container.Column(column =>
            {
                column.Item().Text(title).FontSize(11).Bold().FontColor(HeadingColor);
                column.Item().PaddingTop(2).Height(1.5f).Background(Colors.Grey.Lighten2);
            });
        }

        private void ComposeInvoiceDetails(IContainer container)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem(1).Background(PanelColor).Padding(6).Text("INVOICE NO.").Bold().FontSize(8);
                    row.RelativeItem(2).Padding(6).Text(_invoice.InvoiceNumber).FontSize(9);
                    row.RelativeItem(1).Background(PanelColor).Padding(6).Text("INVOICE DATE").Bold().FontSize(8);
                    row.RelativeItem(2).Padding(6).Text(_invoice.InvoiceDate.ToString("dd/MM/yyyy")).FontSize(9);
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().Row(row =>
                {
                    row.RelativeItem(1).Background(PanelColor).Padding(6).Text("SESSION ID").Bold().FontSize(8);
                    row.RelativeItem(5).Padding(6).Text(_invoice.SessionId).FontSize(9);
                });
            });
        }

        private void ComposeBilledTo(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("BILLED TO").FontSize(10).Bold().FontColor(HeadingColor);
                column.Item().PaddingTop(4).Element(c => ComposeLabelValueRow(c, "NAME", _invoice.BilledToName));
                column.Item().Element(c => ComposeLabelValueRow(c, "PHONE", _invoice.BilledToPhone));
                column.Item().Element(c => ComposeLabelValueRow(c, "EMAIL", _invoice.BilledToEmail));
            });
        }

        private void ComposeBilledBy(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("BILLED BY").FontSize(10).Bold().FontColor(HeadingColor);
                column.Item().PaddingTop(4).Element(c => ComposeLabelValueRow(c, "NAME", CompanyName));
                column.Item().Element(c => ComposeLabelValueRow(c, "GSTIN", CompanyGstin));
                column.Item().Element(c => ComposeLabelValueRow(c, "ADDRESS", CompanyAddress));
                column.Item().Element(c => ComposeLabelValueRow(c, "EMAIL", CompanyEmail));
            });
        }

        private void ComposeLabelValueRow(IContainer container, string label, string value)
        {
            container.PaddingBottom(2).Row(row =>
            {
                row.ConstantItem(65).Text(label).FontSize(8).Bold();
                row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(9);
            });
        }

        private void ComposeSessionInfo(IContainer container)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Column(column =>
            {
                column.Item().Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Element(c => ComposeLabelValueRow(c, "Partner Network", _invoice.PartnerName));
                        col.Item().Element(c => ComposeLabelValueRow(c, "Currency", _invoice.Currency));
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Element(c => ComposeLabelValueRow(c, "Actual Cost", FormatCurrency(_invoice.PartnerCost, _invoice.Currency)));
                        col.Item().Element(c => ComposeLabelValueRow(c, "Energy", $"{_invoice.EnergyConsumedKwh:F2} kWh"));
                    });
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().Background(PanelColor).Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("START TIME").FontSize(8).Bold();
                        col.Item().Text(_invoice.StartTime.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("END TIME").FontSize(8).Bold();
                        col.Item().Text(_invoice.EndTime.HasValue ? _invoice.EndTime.Value.ToString("dd/MM/yyyy HH:mm") : "-").FontSize(9);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("DURATION").FontSize(8).Bold();
                        col.Item().Text(FormatDuration(_invoice.EndTime.HasValue ? _invoice.EndTime.Value - _invoice.StartTime : (TimeSpan?)null)).FontSize(9);
                    });
                });
            });
        }

        private void ComposeFeeTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.7f);
                    columns.RelativeColumn(1.7f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("DESCRIPTION");
                    header.Cell().Element(HeaderCell).Text("SAC CODE");
                    header.Cell().Element(HeaderCell).Text("FEE PER kWh");
                    header.Cell().Element(HeaderCell).Text("UNIT CONSUMED (kWh)");
                    header.Cell().Element(HeaderCell).AlignRight().Text("AMOUNT");

                    IContainer HeaderCell(IContainer c) => c.Background(PanelColor)
                        .BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                        .Padding(5).DefaultTextStyle(x => x.FontSize(7.5f).Bold());
                });

                decimal platformFeeAmount = _invoice.TaxableValue - _invoice.PartnerCost;

                table.Cell().Element(BodyCell).Text("Energy Charges (Partner Network)");
                table.Cell().Element(BodyCell).Text("-");
                table.Cell().Element(BodyCell).Text("-");
                table.Cell().Element(BodyCell).Text($"{_invoice.EnergyConsumedKwh:F2}");
                table.Cell().Element(BodyCell).AlignRight().Text($"{FormatCurrency(_invoice.PartnerCost, _invoice.Currency)}");

                table.Cell().Element(BodyCell).Text(_invoice.Description);
                table.Cell().Element(BodyCell).Text(_invoice.SacCode);
                table.Cell().Element(BodyCell).Text($"{FormatCurrency(_invoice.PricePerUnit)}");
                table.Cell().Element(BodyCell).Text($"{_invoice.EnergyConsumedKwh:F2}");
                table.Cell().Element(BodyCell).AlignRight().Text($"{FormatCurrency(platformFeeAmount)}");

                IContainer BodyCell(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(5).DefaultTextStyle(x => x.FontSize(8.5f));
            });
        }

        private void ComposeBankDetails(IContainer container)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
            {
                column.Item().Text("Bank Details:").Bold().FontSize(9);
                column.Item().Text($"Name: {BankAccountName}").FontSize(8.5f);
                column.Item().Text($"Bank Name: {BankName}").FontSize(8.5f);
                column.Item().Text($"A/C No. {BankAccountNo}").FontSize(8.5f);
                column.Item().Text($"IFSC: {BankIfsc}").FontSize(8.5f);
                column.Item().Text($"Branch: {BankBranch}").FontSize(8.5f);
            });
        }

        private void ComposeTotals(IContainer container)
        {
            decimal platformFeeAmount = _invoice.TaxableValue - _invoice.PartnerCost;

            container.Column(column =>
            {
                column.Item().Element(c => ComposeTotalRow(c, "ACTUAL COST (ENERGY CHARGES)", _invoice.PartnerCost));
                column.Item().Element(c => ComposeTotalRow(c, "PLATFORM FEE", platformFeeAmount));

                column.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().Text("TAXABLE VALUE").Bold().FontSize(9);
                    row.ConstantItem(90).AlignRight().Text(FormatCurrency(_invoice.TaxableValue)).Bold().FontSize(9);
                });

                column.Item().PaddingTop(4).Element(c => ComposeTotalRow(c, $"CGST ({_invoice.CgstRate:0.##}%)", _invoice.CgstAmount));
                column.Item().Element(c => ComposeTotalRow(c, $"SGST ({_invoice.SgstRate:0.##}%)", _invoice.SgstAmount));

                column.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL PAYABLE").Bold().FontSize(10).FontColor(Colors.Green.Darken2);
                    row.ConstantItem(90).AlignRight().Text(FormatCurrency(_invoice.TotalPayable)).Bold().FontSize(10).FontColor(Colors.Green.Darken2);
                });
            });
        }

        private void ComposeTotalRow(IContainer container, string label, decimal value)
        {
            container.PaddingBottom(2).Row(row =>
            {
                row.RelativeItem().Text(label).FontSize(9);
                row.ConstantItem(90).AlignRight().Text(FormatCurrency(value)).FontSize(9);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().PaddingTop(4).Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Medium));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }

        private static string FormatCurrency(decimal value) => $"₹ {value:F2}";

        private static string FormatCurrency(decimal value, string currency)
        {
            if (string.IsNullOrWhiteSpace(currency) || currency.Equals("INR", StringComparison.OrdinalIgnoreCase))
            {
                return FormatCurrency(value);
            }

            return $"{value:F2} {currency}";
        }

        private static string FormatDuration(TimeSpan? duration)
        {
            if (!duration.HasValue)
            {
                return "-";
            }

            var d = duration.Value;
            if (d < TimeSpan.Zero)
            {
                d = TimeSpan.Zero;
            }

            return d.ToString(@"hh\:mm\:ss");
        }
    }
}
