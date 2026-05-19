using System;

namespace LotCatalogFunction.Services
{
    public static class ConnectionHelper
    {
        public static string GetConnectionString()
        {
            return Environment.GetEnvironmentVariable("IFTTEST")
                ?? Environment.GetEnvironmentVariable("SQLCONNSTR_IFTTEST")
                ?? "";
        }
    }
}
