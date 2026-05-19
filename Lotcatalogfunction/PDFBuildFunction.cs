using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Dapper;
using LotCatalogFunction.Models;
using LotCatalogFunction.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace LotCatalogFunction
{
    public class PDFBuildFunction
    {
        private readonly ILogger<PDFBuildFunction> _logger;

        public PDFBuildFunction(ILogger<PDFBuildFunction> logger)
        {
            _logger = logger;
        }

        [Function("PDFBuildFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")]
            HttpRequestData req)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                var connectionString = ConnectionHelper.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    _logger.LogError("Connection string missing.");
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Connection string missing.");
                    return errorResponse;
                }

                using var connection = new SqlConnection(connectionString);

                await connection.OpenAsync();

                var rows = (await connection.QueryAsync<CatalogPdfRow>(@"
                    SELECT
                        CatalogSortOrder,
                        StringNumber,
                        LotNumber,
                        IsShow,
                        SalesType,
                        Gender,
                        [Group],
                        HairLength,
                        Size,
                        Quality,
                        Color,
                        Clarity,
                        Damages,
                        IncludedBoxNumbers,
                        BoxCount,
                        TotalSkins,

                        COUNT(*) OVER
                        (
                            PARTITION BY StringNumber
                        ) AS LotsInString,

                        ROW_NUMBER() OVER
                        (
                            PARTITION BY StringNumber
                            ORDER BY CatalogSortOrder
                        ) AS LotSequenceInString,

                        SUM(TotalSkins) OVER
                        (
                            PARTITION BY StringNumber
                        ) AS StringTotalSkins,

                        SUM(BoxCount) OVER
                        (
                            PARTITION BY StringNumber
                        ) AS StringBoxCount

                    FROM dbo.CatalogLots
                    ORDER BY CatalogSortOrder;
                ")).ToList();

                var sections = rows
                    .GroupBy(BuildSectionTitle)
                    .ToList();

                byte[] pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);

                        page.Margin(25);

                        page.Header().Element(x =>
                        {
                            ComposeHeader(x);
                        });

                        page.Content()
                            .Column(column =>
                            {
                                bool firstSection = true;

                                foreach (var section in sections)
                                {
                                    if (!firstSection)
                                    {
                                        column.Item().PageBreak();
                                    }

                                    firstSection = false;

                                    column.Item()
                                        .Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.ConstantColumn(70);   // Lots
                                                columns.ConstantColumn(55);   // Skins
                                                columns.RelativeColumn();     // Description
                                                columns.ConstantColumn(60);   // Price
                                                columns.ConstantColumn(90);   // Comments
                                            });

                                            table.Header(header =>
                                            {
                                                header.Cell()
                                                    .ColumnSpan(5)
                                                    .Element(SectionCell)
                                                    .Text(section.Key)
                                                    .FontSize(11)
                                                    .Bold();

                                                AddColumnHeader(header);
                                            });

                                            var stringGroups = section
                                                .GroupBy(x => x.StringNumber)
                                                .OrderBy(g => g.Min(x => x.CatalogSortOrder));

                                            foreach (var stringGroup in stringGroups)
                                            {
                                                table.Cell()
                                                    .ColumnSpan(5)
                                                    .Element(x => x.ShowEntire())
                                                    .Table(innerTable =>
                                                    {
                                                        innerTable.ColumnsDefinition(columns =>
                                                        {
                                                            columns.ConstantColumn(70);
                                                            columns.ConstantColumn(55);
                                                            columns.RelativeColumn();
                                                            columns.ConstantColumn(60);
                                                            columns.ConstantColumn(90);
                                                        });

                                                        foreach (var row in stringGroup
                                                            .OrderBy(x => x.CatalogSortOrder))
                                                        {
                                                            AddCatalogRow(innerTable, row);
                                                        }
                                                    });
                                            }
                                        });
                                }
                            });

                        page.Footer().Element(x =>
                        {
                            ComposeFooter(x);
                        });
                    });

                }).GeneratePdf();

                _logger.LogInformation("PDF generated: {Sections} sections, {Rows} rows",
                    sections.Count, rows.Count);

                var response = req.CreateResponse(HttpStatusCode.OK);

                response.Headers.Add(
                    "Content-Type",
                    "application/pdf"
                );

                response.Headers.Add(
                    "Content-Disposition",
                    "attachment; filename=lot-catalog.pdf"
                );

                await response.Body.WriteAsync(
                    pdfBytes,
                    0,
                    pdfBytes.Length
                );

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PDFBuildFunction");
                var response = req.CreateResponse(
                    HttpStatusCode.InternalServerError
                );

                await response.WriteStringAsync(ex.ToString());

                return response;
            }
        }

        private static void AddColumnHeader(TableCellDescriptor table)
        {
            table.Cell()
                .Element(HeaderCell)
                .Text("Lots")
                .Bold();

            table.Cell()
                .Element(HeaderCell)
                .Text("Skins")
                .Bold();

            table.Cell()
                .Element(HeaderCell)
                .Text("Description")
                .Bold();

            table.Cell()
                .Element(HeaderCell)
                .Text("Price")
                .Bold();

            table.Cell()
                .Element(HeaderCell)
                .Text("Comments")
                .Bold();
        }

        private static void AddCatalogRow(
            TableDescriptor table,
            CatalogPdfRow row)
        {
            table.Cell()
                .Element(x => MultiStringCell(x, row, isLeftEdge: true))
                .Text(BuildLotsText(row));

            table.Cell()
                .Element(x => MultiStringCell(x, row))
                .Text(BuildSkinsText(row));

            table.Cell()
                .Element(x => MultiStringCell(x, row))
                .Text(BuildDescriptionText(row));

            table.Cell()
                .Element(x => MultiStringCell(x, row, isRightEdge: true))
                .Text("");

            table.Cell()
                .Element(BodyCell)
                .Text("");
        }

        private static void ComposeHeader(IContainer container)
        {
            container
                .PaddingBottom(15)
                .Row(row =>
                {
                    row.RelativeItem()
                        .AlignBottom()
                        .Column(column =>
                        {
                            column.Item()
                                .AlignLeft()
                                .Text(Environment.GetEnvironmentVariable("CATALOG_HEADER_TEXT") ?? "261 JULY 26")
                                .FontSize(9)
                                .Bold();
                        });

                    row.ConstantItem(120)
                        .Height(45)
                        .AlignRight()
                        .AlignBottom()
                        .Image(
                            "Assets/kopenhagenfur-logo.png",
                            ImageScaling.FitArea
                        );
                });
        }

        private static void ComposeFooter(IContainer container)
        {
            container
                .AlignCenter()
                .Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
        }

        private static string BuildSectionTitle(CatalogPdfRow row)
        {
            return
                $"{row.SalesType} - " +
                $"{row.Gender} - " +
                $"{row.Group}";
        }

        private static string BuildLotsText(CatalogPdfRow row)
        {
            return row.LotNumber.ToString();
        }

        private static string BuildSkinsText(CatalogPdfRow row)
        {
            return row.TotalSkins.ToString("#,##0");
        }

        private static string BuildDescriptionText(CatalogPdfRow row)
        {
            if (!row.IsMultiLotString)
            {
                return BuildDescription(row);
            }

            if (row.LotSequenceInString == 1)
            {
                return BuildDescription(row);
            }

            if (row.IsLastLotInString)
            {
                return $"{row.StringTotalSkins:#,##0} skins";
            }

            return row.LotSequenceInString.ToString();
        }

        private static string BuildDescription(CatalogPdfRow row)
        {
            var parts = new List<string>
            {
                row.HairLength,
                row.Size,
                row.Quality,
                row.Color,
                row.Clarity
            };

            if (
                !string.IsNullOrWhiteSpace(row.Damages)
                && !string.Equals(
                    row.Damages,
                    "None",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                parts.Add(row.Damages);
            }

            return string.Join(
                " / ",
                parts.Where(x => !string.IsNullOrWhiteSpace(x))
            );
        }

        private static IContainer MultiStringCell(
            IContainer container,
            CatalogPdfRow row,
            bool isLeftEdge = false,
            bool isRightEdge = false)
        {
            var cell = container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten1)
                .Background(Colors.White)
                .PaddingVertical(3)
                .PaddingHorizontal(4);

            if (!row.IsMultiLotString)
            {
                return cell;
            }

            if (row.LotSequenceInString == 1)
            {
                cell = cell
                    .BorderTop(1.25f)
                    .BorderColor(Colors.Black);
            }

            if (row.IsLastLotInString)
            {
                cell = cell
                    .BorderBottom(1.25f)
                    .BorderColor(Colors.Black);
            }

            if (isLeftEdge)
            {
                cell = cell
                    .BorderLeft(1.25f)
                    .BorderColor(Colors.Black);
            }

            if (isRightEdge)
            {
                cell = cell
                    .BorderRight(1.25f)
                    .BorderColor(Colors.Black);
            }

            return cell;
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Medium)
                .Background(Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .PaddingHorizontal(4);
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten1)
                .Background(Colors.White)
                .PaddingVertical(3)
                .PaddingHorizontal(4);
        }

        private static IContainer SectionCell(IContainer container)
        {
            return container
                .Border(0.75f)
                .BorderColor(Colors.Grey.Darken1)
                .Background(Colors.Grey.Lighten2)
                .PaddingVertical(5)
                .PaddingHorizontal(4);
        }
    }
}