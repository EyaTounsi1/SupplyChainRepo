using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using PartTracker.Models;
using PartTracker.Shared.Services;

namespace PartTracker;

public sealed class SafetyStockSnapshotResult
{
    public DateTime? SnapshotDate { get; init; }
    public List<SafetyStockItem> Items { get; init; } = new();
}

public class SafetyStockService : ISafetyStockService
{

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Lightweight query (fast + safe)
            var dt = await _snowflake.QueryAsync("SELECT 1;");
            return dt.Rows.Count > 0;
        }
        catch
        {
            return false;
        }
    }
    private readonly SnowflakeService _snowflake;

    // Fixed values per your requirement
    private const string Site = "VCT";
    private const string PlanningPoint = "1003";

    public SafetyStockService(SnowflakeService snowflake)
    {
        _snowflake = snowflake;
    }

    // Implement missing ISafetyStockService methods for legacy interface compatibility
    public async Task<List<SafetyStockItem>> GetSafetyStockDataAsync()
    {
        var result = await GetSafetyStockVct1003Async();
        return result.Items;
    }

    public Task<List<SafetyStockChange>> GetSafetyStockChangesAsync()
    {
        return Task.FromResult(new List<SafetyStockChange>());
    }

    public Task<List<SafetyStockChange>> GetSafetyStockChangesAsync(string site, string planningPoint)
    {
        return Task.FromResult(new List<SafetyStockChange>());
    }

    public async Task<SafetyStockSnapshotResult> GetSafetyStockVct1003Async()
    {
        // Take a consistent snapshot: pick the latest UPLOADED_FROM_SOURCE for VCT
        // then return rows for planning point 1003 on that snapshot date.
        var sql = @"
WITH picked AS (
    SELECT MAX(UPLOADED_FROM_SOURCE) AS snapshot_dt
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED
    WHERE SITE = 'VCT'
),
data AS (
    SELECT
        SITE,
        PLANNING_POINT,
        PART_NUMBER,
        MFG_SUPPLIER_CODE,
        SAFETY_STOCK_NR_OF_PARTS,
        UPLOADED_FROM_SOURCE
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED
    WHERE SITE = 'VCT'
      AND PLANNING_POINT = '1003'
      AND UPLOADED_FROM_SOURCE = (SELECT snapshot_dt FROM picked)
)
SELECT *
FROM data
ORDER BY PART_NUMBER, MFG_SUPPLIER_CODE;";

        var dt = await _snowflake.QueryAsync(sql);

        DateTime? snapshotDate = null;
        if (dt.Rows.Count > 0)
            snapshotDate = dt.Rows[0].Field<DateTime?>("UPLOADED_FROM_SOURCE");

        var items = new List<SafetyStockItem>();
        foreach (DataRow row in dt.Rows)
        {
            items.Add(new SafetyStockItem
            {
                PartNumber = row.Field<string>("PART_NUMBER") ?? string.Empty,
                MfgSupplierCode = row.Field<string>("MFG_SUPPLIER_CODE") ?? string.Empty,
                SafetyStockNrOfParts = Convert.ToSingle(row["SAFETY_STOCK_NR_OF_PARTS"] ?? 0f),
            });
        }

        return new SafetyStockSnapshotResult
        {
            SnapshotDate = snapshotDate,
            Items = items
        };
    }

}
