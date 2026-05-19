using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Dapper;
using LotCatalogFunction.Models;
using LotCatalogFunction.Services;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace LotCatalogFunction
{
    public class GenerateLotsFunction
    {
        [Function("GenerateLots")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")]
            HttpRequestData req)
        {
            try
            {
                string connectionString =
                    Environment.GetEnvironmentVariable("IFTTEST")
                    ?? Environment.GetEnvironmentVariable("SQLCONNSTR_IFTTEST");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await response.WriteStringAsync("Connection string missing.");
                    return response;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var boxes = await connection.QueryAsync<BoxRow>(@"
                    SELECT
                        BoxNumber,
                        BoxType,
                        SalesType,
                        Gender,
                        [Group],
                        Damages,
                        CAST(Size AS nvarchar(50)) AS Size,
                        HairLength,
                        Color,
                        Quality,
                        Clarity,
                        Skins
                    FROM dbo.Boxes
                    WHERE Skins IS NOT NULL;
                ");

                var rules = await connection.QueryAsync<LotSizeRule>(@"
                    SELECT
                        RuleID,
                        Gender,
                        Size,
                        MaxBoxes,
                        MaxSkinsPerBox,
                        ShowlotSkins,
                        MaxLotSizeExclShowlot,
                        MaxLotSizeInclShowlot,
                        Priority,
                        IsActive
                    FROM dbo.LotSizeRule
                    WHERE IsActive = 1
                    ORDER BY Priority;
                ");

                var groupOrders = await connection.QueryAsync<LotGroupOrderFunction>(@"
                    SELECT
                        ColumnName,
                        GroupOrder
                    FROM dbo.LotGroupOrder
                    ORDER BY GroupOrder;
                ");

                var sortOrders = await connection.QueryAsync<LotSortOrder>(@"
                    SELECT
                        ColumnName,
                        Value,
                        SortOrder
                    FROM dbo.LotSortOrder;
                ");

                var stringDefinitions = await connection.QueryAsync<StringDefinition>(@"
                    SELECT
                        StringDefinitionID,
                        ColumnName,
                        IsActive
                    FROM dbo.StringDefinition
                    WHERE IsActive = 1;
                ");

                var catalogNumberRules = await connection.QueryAsync<CatalogNumberRule>(@"
                    SELECT
                        CatalogNumberRuleID,
                        SalesType,
                        Gender,
                        [Group],
                        StartNumber,
                        IsActive
                    FROM dbo.CatalogNumberRule
                    WHERE IsActive = 1;
                ");

                var lotGenerationService = new LotGenerationService();

                var generationResult = lotGenerationService.GenerateLots(
                    boxes,
                    rules,
                    groupOrders,
                    sortOrders
                );

                var lots = generationResult.Lots;
                var skippedGroups = generationResult.SkippedGroups;

                var catalogBuildService = new CatalogBuildService();

                var catalogLots = catalogBuildService.BuildCatalogLots(
                    lots,
                    stringDefinitions,
                    groupOrders,
                    sortOrders,
                    catalogNumberRules
                );

                using var transaction = connection.BeginTransaction();

                await connection.ExecuteAsync(
                    "TRUNCATE TABLE dbo.Lots;",
                    transaction: transaction
                );

                await connection.ExecuteAsync(
                    "TRUNCATE TABLE dbo.LotGenerationSkippedGroup;",
                    transaction: transaction
                );

                await connection.ExecuteAsync(
                    "TRUNCATE TABLE dbo.CatalogLots;",
                    transaction: transaction
                );

                if (lots.Any())
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO dbo.Lots
                        (
                            UniqueID,
                            IsShow,
                            ShowlotBoxNumber,
                            SalesType,
                            [Group],
                            Gender,
                            Size,
                            Color,
                            Quality,
                            Clarity,
                            HairLength,
                            Damages,
                            IncludedBoxNumbers,
                            BoxCount,
                            TotalSkins
                        )
                        VALUES
                        (
                            @UniqueID,
                            @IsShow,
                            @ShowlotBoxNumber,
                            @SalesType,
                            @Group,
                            @Gender,
                            @Size,
                            @Color,
                            @Quality,
                            @Clarity,
                            @HairLength,
                            @Damages,
                            @IncludedBoxNumbers,
                            @BoxCount,
                            @TotalSkins
                        );
                    ", lots, transaction: transaction);
                }

                if (catalogLots.Any())
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO dbo.CatalogLots
                        (
                            LotUniqueID,
                            StringNumber,
                            LotNumber,
                            CatalogSortOrder,
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
                            TotalSkins
                        )
                        VALUES
                        (
                            @LotUniqueID,
                            @StringNumber,
                            @LotNumber,
                            @CatalogSortOrder,
                            @IsShow,
                            @SalesType,
                            @Gender,
                            @Group,
                            @HairLength,
                            @Size,
                            @Quality,
                            @Color,
                            @Clarity,
                            @Damages,
                            @IncludedBoxNumbers,
                            @BoxCount,
                            @TotalSkins
                        );
                    ", catalogLots, transaction: transaction);
                }

                if (skippedGroups.Any())
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO dbo.LotGenerationSkippedGroup
                        (
                            RunID,
                            Reason,
                            SalesType,
                            [Group],
                            Gender,
                            Size,
                            Color,
                            Quality,
                            Clarity,
                            HairLength,
                            Damages,
                            BoxCount,
                            ShowlotCount,
                            TotalSkins,
                            BoxNumbers
                        )
                        VALUES
                        (
                            @RunID,
                            @Reason,
                            @SalesType,
                            @Group,
                            @Gender,
                            @Size,
                            @Color,
                            @Quality,
                            @Clarity,
                            @HairLength,
                            @Damages,
                            @BoxCount,
                            @ShowlotCount,
                            @TotalSkins,
                            @BoxNumbers
                        );
                    ", skippedGroups, transaction: transaction);
                }

                transaction.Commit();

                var ok = req.CreateResponse(HttpStatusCode.OK);

                await ok.WriteStringAsync(
                    $"Generated lots: {lots.Count}. " +
                    $"Catalog lots: {catalogLots.Count}. " +
                    $"Skipped groups: {skippedGroups.Count}."
                );

                return ok;
            }
            catch (Exception ex)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync(ex.ToString());
                return response;
            }
        }
    }
}