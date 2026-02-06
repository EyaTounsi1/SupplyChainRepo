using Microsoft.Extensions.Configuration;
using System.Data;
using PartTracker.Shared.Services;
using PartTracker.Models;

namespace PartTracker
{
    public class SafetyStockService : ISafetyStockService
    {
        private readonly SnowflakeService _snowflake;

        public SafetyStockService(SnowflakeService snowflake, IConfiguration config)
        {
            _snowflake = snowflake;
        }

        // SAFETY_STOCK_SETTINGS_AS_MANUFACTURED
        public Task<DataTable> GetSafetyStockSettingsAsync()
        {
            var sql = """
                SELECT *
                FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED
            """;

            return _snowflake.QueryAsync(sql);
        }

    
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var result = await _snowflake.QueryAsync("SELECT 1");
                return result != null && result.Rows.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        // Interface implementations
        public Task<List<SafetyStockItem>> GetSafetyStockDataAsync() => Task.FromResult(new List<SafetyStockItem>());
        public Task<List<SafetyStockChange>> GetSafetyStockChangesAsync() => Task.FromResult(new List<SafetyStockChange>());
        public Task<List<SafetyStockChange>> GetSafetyStockChangesAsync(string site, string planningPoint) => Task.FromResult(new List<SafetyStockChange>());
    }
}
