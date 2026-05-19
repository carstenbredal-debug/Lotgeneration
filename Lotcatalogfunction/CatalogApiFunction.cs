using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Dapper;
using LotCatalogFunction.Services;
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
                        SalesType,
                        Gender,
                        [Group],
                        Color,
                        Quality,
                        Clarity,
                        Size,
                        TotalSkins,
                        BoxCount,
                        IsShow
                    FROM dbo.CatalogLots
                    WHERE 1=1";

                var parameters = new DynamicParameters();

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
    }
}
