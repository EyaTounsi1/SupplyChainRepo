using Microsoft.Extensions.Configuration;
using PartTracker.Models;
using Snowflake.Data.Client;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PartTracker;

public class SafetyStockService : ISafetyStockService
{
    private readonly string _connectionString;

    public SafetyStockService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SnowflakeConnection") ?? throw new InvalidOperationException("Snowflake connection string not found.");
    }

    public async Task<List<SafetyStockItem>> GetSafetyStockDataAsync()
    {
        var items = new List<SafetyStockItem>();

        using (var conn = new SnowflakeDbConnection())
        {
            conn.ConnectionString = _connectionString;
            await conn.OpenAsync();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT PART_NUMBER, SAFETY_STOCK_NR_OF_PARTS FROM SAFETY_STOCK_SETTINGS_AS_MANUFACTURED WHERE SITE = 'VCT'";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var item = new SafetyStockItem
                        {
                            PartNumber = reader.GetString(0),
                            SafetyStockNrOfParts = reader.GetFloat(1)
                        };
                        items.Add(item);
                    }
                }
            }
        }

        return items;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using (var conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = _connectionString;
                await conn.OpenAsync();
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
public class AnotherSnowflakeService
{
    private readonly string _connectionString;

    public AnotherSnowflakeService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SnowflakeConnection2") ?? throw new InvalidOperationException("Snowflake connection string not found.");
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using (var conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = _connectionString;
                await conn.OpenAsync();
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    // Add your data retrieval methods here
}