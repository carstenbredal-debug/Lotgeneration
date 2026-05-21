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
using System.Text.Json;
using System.Threading.Tasks;

namespace LotCatalogFunction
{
    public class CatalogApiFunction
    {
        private readonly ILogger<CatalogApiFunction> _logger;

        public CatalogApiFunction(ILogger<CatalogApiFunction> logger)
        {
            _logger = logger;
        }

        [Function("GetFilters")]
        public async Task<HttpResponseData> GetFilters(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/filters")]
            HttpRequestData req)
        {
            try
            {
                var connectionString = ConnectionHelper.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    _logger.LogError("Connection string missing.");
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync("Connection string missing.");
                    return err;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var filters = new Dictionary<string, List<string>>();

                filters["types"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT SalesType FROM dbo.CatalogLots WHERE SalesType IS NOT NULL ORDER BY SalesType"
                )).ToList();

                filters["genders"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT Gender FROM dbo.CatalogLots WHERE Gender IS NOT NULL ORDER BY Gender"
                )).ToList();

                filters["groups"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT [Group] FROM dbo.CatalogLots WHERE [Group] IS NOT NULL ORDER BY [Group]"
                )).ToList();

                filters["colors"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT Color FROM dbo.CatalogLots WHERE Color IS NOT NULL ORDER BY Color"
                )).ToList();

                filters["qualities"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT Quality FROM dbo.CatalogLots WHERE Quality IS NOT NULL ORDER BY Quality"
                )).ToList();

                filters["clarities"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT Clarity FROM dbo.CatalogLots WHERE Clarity IS NOT NULL ORDER BY Clarity"
                )).ToList();

                filters["sizes"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT Size FROM dbo.CatalogLots WHERE Size IS NOT NULL ORDER BY Size"
                )).ToList();

                filters["hairLengths"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT HairLength FROM dbo.CatalogLots WHERE HairLength IS NOT NULL ORDER BY HairLength"
                )).ToList();

                filters["damages"] = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT Damages FROM dbo.CatalogLots WHERE Damages IS NOT NULL ORDER BY Damages"
                )).ToList();

                _logger.LogInformation("Filters loaded.");

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(filters));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading filters");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync(ex.Message);
                return response;
            }
        }

        [Function("GetCatalogLots")]
        public async Task<HttpResponseData> GetCatalogLots(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/catalog-lots")]
            HttpRequestData req)
        {
            try
            {
                var connectionString = ConnectionHelper.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync("Connection string missing.");
                    return err;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

                var sql = @"
                    SELECT
                        LotNumber,
                        StringNumber,
                        CatalogSortOrder,
                        SalesType,
                        Gender,
                        [Group],
                        Color,
                        Quality,
                        Clarity,
                        Size,
                        HairLength,
                        Damages,
                        TotalSkins,
                        BoxCount,
                        CASE WHEN IsShow = 'Yes' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsShow,

                        COUNT(*) OVER (
                            PARTITION BY StringNumber
                        ) AS LotsInString,

                        ROW_NUMBER() OVER (
                            PARTITION BY StringNumber
                            ORDER BY CatalogSortOrder
                        ) AS LotSequenceInString,

                        SUM(TotalSkins) OVER (
                            PARTITION BY StringNumber
                        ) AS StringTotalSkins

                    FROM dbo.CatalogLots
                    WHERE 1=1";

                var parameters = new DynamicParameters();

                AddFilterParameters(query, ref sql, parameters);

                sql += " ORDER BY CatalogSortOrder";

                var lots = (await connection.QueryAsync(sql, parameters)).ToList();

                _logger.LogInformation("Catalog lots loaded: {Count}", lots.Count);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(lots));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading catalog lots");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync(ex.Message);
                return response;
            }
        }

        [Function("GenerateCount")]
        public async Task<HttpResponseData> GenerateCount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/generate")]
            HttpRequestData req)
        {
            try
            {
                var connectionString = ConnectionHelper.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync("Connection string missing.");
                    return err;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

                var sql = "SELECT COUNT(*) FROM dbo.CatalogLots WHERE 1=1";
                var parameters = new DynamicParameters();

                AddFilterParameters(query, ref sql, parameters);

                var count = await connection.ExecuteScalarAsync<int>(sql, parameters);

                _logger.LogInformation("Generate count: {Count}", count);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Found {count} catalog lots matching your filters.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenerateCount");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync(ex.Message);
                return response;
            }
        }

        [Function("GeneratePdf")]
        public async Task<HttpResponseData> GeneratePdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/generate-pdf")]
            HttpRequestData req)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                var connectionString = ConnectionHelper.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync("Connection string missing.");
                    return err;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

                var sql = @"
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

                        COUNT(*) OVER (
                            PARTITION BY StringNumber
                        ) AS LotsInString,

                        ROW_NUMBER() OVER (
                            PARTITION BY StringNumber
                            ORDER BY CatalogSortOrder
                        ) AS LotSequenceInString,

                        SUM(TotalSkins) OVER (
                            PARTITION BY StringNumber
                        ) AS StringTotalSkins,

                        SUM(BoxCount) OVER (
                            PARTITION BY StringNumber
                        ) AS StringBoxCount

                    FROM dbo.CatalogLots
                    WHERE 1=1";

                var parameters = new DynamicParameters();
                AddFilterParameters(query, ref sql, parameters);

                sql += " ORDER BY CatalogSortOrder";

                var rows = (await connection.QueryAsync<CatalogPdfRow>(sql, parameters)).ToList();

                if (!rows.Any())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("No lots found matching your filters.");
                    return notFound;
                }

                var sections = rows
                    .GroupBy(r => $"{r.SalesType} - {r.Gender} - {r.Group}")
                    .ToList();

                byte[] pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(25);

                        page.Header().Element(ComposeHeader);

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
                                                columns.ConstantColumn(70);
                                                columns.ConstantColumn(55);
                                                columns.RelativeColumn();
                                                columns.ConstantColumn(90);
                                            });

                                            table.Header(header =>
                                            {
                                                header.Cell()
                                                    .ColumnSpan(4)
                                                    .Element(SectionCell)
                                                    .Text(section.Key)
                                                    .FontSize(11)
                                                    .Bold();

                                                AddColumnHeader(header);
                                            });

                                            var stringGroups = section
                                                .GroupBy(x => x.StringNumber)
                                                .OrderBy(g => g.Min(x => x.CatalogSortOrder));

                                            var allEntries = new List<(CatalogPdfRow row, bool isMultiLot, bool isFirst, bool isLast)>();
                                            foreach (var stringGroup in stringGroups)
                                            {
                                                var grpRows = stringGroup
                                                    .OrderBy(x => x.CatalogSortOrder)
                                                    .ToList();
                                                bool isMultiLot = grpRows.Any(r => r.IsMultiLotString);
                                                for (int i = 0; i < grpRows.Count; i++)
                                                    allEntries.Add((grpRows[i], isMultiLot, i == 0, i == grpRows.Count - 1));
                                            }

                                            for (int idx = 0; idx < allEntries.Count; idx++)
                                            {
                                                var (row, isMultiLot, isFirst, isLast) = allEntries[idx];
                                                bool nextIsMultiLotStart = idx + 1 < allEntries.Count
                                                    && allEntries[idx + 1].isMultiLot
                                                    && allEntries[idx + 1].isFirst;
                                                AddCatalogRow(table, row, isMultiLot, isFirst, isLast, nextIsMultiLotStart);
                                            }
                                        });
                                }
                            });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf();

                _logger.LogInformation("PDF generated: {Sections} sections, {Rows} rows",
                    sections.Count, rows.Count);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                response.Headers.Add("Content-Disposition", "attachment; filename=lot-catalog.pdf");
                await response.Body.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync(ex.ToString());
                return response;
            }
        }

        private static void AddFilterParameters(
            System.Collections.Specialized.NameValueCollection query,
            ref string sql,
            DynamicParameters parameters)
        {
            if (!string.IsNullOrEmpty(query["type"]))
            {
                sql += " AND SalesType = @SalesType";
                parameters.Add("SalesType", query["type"]);
            }
            if (!string.IsNullOrEmpty(query["gender"]))
            {
                sql += " AND Gender = @Gender";
                parameters.Add("Gender", query["gender"]);
            }
            if (!string.IsNullOrEmpty(query["group"]))
            {
                sql += " AND [Group] = @Group";
                parameters.Add("Group", query["group"]);
            }
            if (!string.IsNullOrEmpty(query["color"]))
            {
                sql += " AND Color = @Color";
                parameters.Add("Color", query["color"]);
            }
            if (!string.IsNullOrEmpty(query["quality"]))
            {
                sql += " AND Quality = @Quality";
                parameters.Add("Quality", query["quality"]);
            }
            if (!string.IsNullOrEmpty(query["clarity"]))
            {
                sql += " AND Clarity = @Clarity";
                parameters.Add("Clarity", query["clarity"]);
            }
            if (!string.IsNullOrEmpty(query["size"]))
            {
                sql += " AND Size = @Size";
                parameters.Add("Size", query["size"]);
            }
            if (!string.IsNullOrEmpty(query["damage"]))
            {
                sql += " AND Damages = @Damages";
                parameters.Add("Damages", query["damage"]);
            }
            if (!string.IsNullOrEmpty(query["hairLength"]))
            {
                sql += " AND HairLength = @HairLength";
                parameters.Add("HairLength", query["hairLength"]);
            }
        }

        private static void AddColumnHeader(TableCellDescriptor table)
        {
            table.Cell().Element(HeaderCell).Text("Lots").Bold();
            table.Cell().Element(HeaderCell).Text("Skins").Bold();
            table.Cell().Element(HeaderCell).Text("Description").Bold();
            table.Cell().Element(HeaderCell).Text("Comments").Bold();
        }

        private static void AddCatalogRow(
            TableDescriptor table, CatalogPdfRow row,
            bool isMultiLot, bool isFirst, bool isLast,
            bool nextIsMultiLotStart = false)
        {
            if (!isMultiLot)
            {
                IContainer NormalCell(IContainer c)
                {
                    var s = c.Background(Colors.White).PaddingVertical(3).PaddingHorizontal(4);
                    if (!nextIsMultiLotStart)
                        s = s.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1);
                    return s;
                }
                table.Cell().Element(NormalCell).Text(row.LotNumber.ToString());
                table.Cell().Element(NormalCell).Text(row.TotalSkins.ToString("#,##0"));
                table.Cell().Element(NormalCell).Text(BuildDescriptionText(row));
                table.Cell().Element(NormalCell).Text("");
                return;
            }

            for (int col = 0; col < 4; col++)
            {
                string text = col switch
                {
                    0 => row.LotNumber.ToString(),
                    1 => row.TotalSkins.ToString("#,##0"),
                    2 => BuildDescriptionText(row),
                    _ => ""
                };

                var cell = table.Cell();
                var container = cell
                    .Background(Colors.White)
                    .PaddingVertical(3)
                    .PaddingHorizontal(4);

                if (isFirst)
                    container = container.BorderTop(1.5f).BorderColor(Colors.Black);
                if (isLast)
                    container = container.BorderBottom(1.5f).BorderColor(Colors.Black);
                else
                    container = container.BorderBottom(0.5f).BorderColor(Colors.Black);
                if (col == 0)
                    container = container.BorderLeft(1.5f).BorderColor(Colors.Black);
                if (col == 3)
                    container = container.BorderRight(1.5f).BorderColor(Colors.Black);

                container.Text(text);
            }
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
                        .Image("Assets/kopenhagenfur-logo.png", ImageScaling.FitArea);
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
                    text.Span("  [BUILD-V9]");
                });
        }

        private static string BuildDescriptionText(CatalogPdfRow row)
        {
            if (!row.IsMultiLotString)
                return BuildDescription(row);

            if (row.LotSequenceInString == 1)
                return BuildDescription(row);

            if (row.IsLastLotInString)
                return $"{row.StringTotalSkins:#,##0} skins";

            return row.LotSequenceInString.ToString();
        }

        private static string BuildDescription(CatalogPdfRow row)
        {
            var parts = new List<string>
            {
                row.HairLength, row.Size, row.Quality, row.Color, row.Clarity
            };

            if (!string.IsNullOrWhiteSpace(row.Damages)
                && !string.Equals(row.Damages, "None", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(row.Damages);
            }

            return string.Join(" / ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
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
