using System;
using System.IO;
using OCPP.Core.Database.EVCDTO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OCPP.Core.Management.Services.Invoice
{
    public class InvoiceDocument : IDocument
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

        private readonly SessionInvoice _invoice;
        private readonly string _logoPath;

        public InvoiceDocument(SessionInvoice invoice, string logoPath = null)
        {
            _invoice = invoice;
            _logoPath = logoPath;
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
                    row.ConstantItem(110).Height(45).Element(ComposeLogo);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignRight().Text("INVOICE").FontSize(22).Bold().FontColor(HeadingColor);
                        col.Item().AlignRight().Text("Invoice Type: Tax Invoice").FontSize(9).FontColor(MutedColor);
                    });
                });

                column.Item().PaddingTop(8).Height(3).Background(BrandColor);
            });
        }

        private void ComposeLogo(IContainer container)
        {
            if (!string.IsNullOrEmpty(_logoPath) && File.Exists(_logoPath))
            {
                container.Image(_logoPath).FitArea();
            }
            else
            {
                // Placeholder until the ORT company logo file is provided (see InvoiceService logo path)
                container.Border(1)
                    .BorderColor(Colors.Grey.Lighten1)
                    .AlignMiddle()
                    .AlignCenter()
                    .Text("ORT LOGO")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Medium);
            }
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

                column.Item().Element(c => ComposeSectionHeading(c, "STATION INFO"));
                column.Item().Element(ComposeStationInfo);

                column.Item().Element(c => ComposeSectionHeading(c, "CHARGING DETAILS"));
                column.Item().Element(ComposeChargingTable);

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
                    text.Span("This is a system-generated invoice for EV charging service. Amounts are in INR.");
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
                    row.RelativeItem(5).Padding(6).Text(_invoice.ChargingSessionId).FontSize(9);
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

        private void ComposeStationInfo(IContainer container)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Column(column =>
            {
                column.Item().Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Element(c => ComposeLabelValueRow(c, "Hub", _invoice.ChargingHubName));
                        col.Item().Element(c => ComposeLabelValueRow(c, "Charge Point", _invoice.ChargePointId));
                        col.Item().Element(c => ComposeLabelValueRow(c, "Charger Type", _invoice.ChargerType));
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Element(c => ComposeLabelValueRow(c, "City", _invoice.City));
                        col.Item().Element(c => ComposeLabelValueRow(c, "Connector", _invoice.ConnectorId));
                        col.Item().Element(c => ComposeLabelValueRow(c, "Power Output", FormatPowerOutput(_invoice.PowerOutput)));
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
                        col.Item().Text(_invoice.EndTime.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("DURATION").FontSize(8).Bold();
                        col.Item().Text(FormatDuration(_invoice.EndTime - _invoice.StartTime)).FontSize(9);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("ENERGY").FontSize(8).Bold();
                        col.Item().Text($"{_invoice.EnergyConsumedKwh:F2} kWh").FontSize(9);
                    });
                });
            });
        }

        private void ComposeChargingTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.7f);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.7f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("DESCRIPTION");
                    header.Cell().Element(HeaderCell).Text("SAC CODE");
                    header.Cell().Element(HeaderCell).Text("PRICE PER UNIT");
                    header.Cell().Element(HeaderCell).Text("UNIT CONSUMED (kWh)");
                    header.Cell().Element(HeaderCell).Text("CHARGED ON");
                    header.Cell().Element(HeaderCell).Text("DURATION");
                    header.Cell().Element(HeaderCell).AlignRight().Text("AMOUNT");

                    IContainer HeaderCell(IContainer c) => c.Background(PanelColor)
                        .BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                        .Padding(5).DefaultTextStyle(x => x.FontSize(7.5f).Bold());
                });

                table.Cell().Element(BodyCell).Text(_invoice.Description);
                table.Cell().Element(BodyCell).Text(_invoice.SacCode);
                table.Cell().Element(BodyCell).Text($"{FormatCurrency(_invoice.PricePerUnit)}");
                table.Cell().Element(BodyCell).Text($"{_invoice.EnergyConsumedKwh:F2}");
                table.Cell().Element(BodyCell).Text(_invoice.StartTime.ToString("dd/MM/yyyy HH:mm"));
                table.Cell().Element(BodyCell).Text(FormatDuration(_invoice.EndTime - _invoice.StartTime));
                table.Cell().Element(BodyCell).AlignRight().Text($"{FormatCurrency(_invoice.TaxableValue)}");

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
            container.Column(column =>
            {
                column.Item().Element(c => ComposeTotalRow(c, "TAXABLE VALUE", _invoice.TaxableValue));
                column.Item().Element(c => ComposeTotalRow(c, "DISCOUNT", _invoice.Discount));
                column.Item().Element(c => ComposeTotalRow(c, "CASHBACK", _invoice.Cashback));
                column.Item().Element(c => ComposeTotalRow(c, $"CGST ({_invoice.CgstRate:0.##}%)", _invoice.CgstAmount));
                column.Item().Element(c => ComposeTotalRow(c, $"SGST ({_invoice.SgstRate:0.##}%)", _invoice.SgstAmount));

                column.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().Text("GRAND TOTAL").Bold().FontSize(10).FontColor(Colors.Green.Darken2);
                    row.ConstantItem(90).AlignRight().Text(FormatCurrency(_invoice.GrandTotal)).Bold().FontSize(10).FontColor(Colors.Green.Darken2);
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

        private static string FormatPowerOutput(string powerOutput)
        {
            if (string.IsNullOrWhiteSpace(powerOutput))
            {
                return "-";
            }

            return powerOutput.Contains("kW", StringComparison.OrdinalIgnoreCase)
                ? powerOutput
                : $"{powerOutput} kW";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            return duration.ToString(@"hh\:mm\:ss");
        }
    }
}
