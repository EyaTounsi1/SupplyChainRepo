using Microsoft.Extensions.Configuration;
using PartTracker.Models;
using Snowflake.Data.Client;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PartTracker.Shared.Services;

namespace PartTracker;

public class SafetyStockService : ISafetyStockService
{
    private readonly SnowflakeService _snowflake;

    public SafetyStockService(SnowflakeService snowflake)
    {
        _snowflake = snowflake;
    }

    public async Task<List<SafetyStockItem>> GetSafetyStockDataAsync()
    {
        var items = new List<SafetyStockItem>();

        // Query via JWT-configured SnowflakeService
        var dt = await _snowflake.QueryAsync(
            "SELECT SHP_SUPPLIER_CODE FROM MANUFACTURING_BUSINESS_DATA_PRODUCTS.INVENTORY_BY_PURPOSE.DEMAND_FLUCTUATION_INPUT_INV_MATRIX LIMIT 100"
        );

        foreach (DataRow row in dt.Rows)
        {
            var item = new SafetyStockItem
            {
                PartNumber = row.Field<string>(0)!,
                SafetyStockNrOfParts = Convert.ToSingle(row[1])
            };
            items.Add(item);
        }

        return items;
    }

        public async Task<List<SafetyStockChange>> GetSafetyStockChangesAsync()
        {
                return await GetSafetyStockChangesAsync("VCT", "1003");
        }

        public async Task<List<SafetyStockChange>> GetSafetyStockChangesAsync(string site, string planningPoint)
        {
                // Basic sanitization to avoid SQL injection in interpolated literals
                string Sanitize(string s)
                {
                        var trimmed = (s ?? string.Empty).Trim();
                        foreach (var ch in trimmed)
                        {
                                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ))
                                        throw new InvalidOperationException("Invalid characters in filter");
                        }
                        return trimmed;
                }

                site = Sanitize(site);
                planningPoint = Sanitize(planningPoint);

                var sql = $@"WITH dates AS (
    SELECT DISTINCT uploaded_from_source
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL
    WHERE site = '{site}'
),
picked AS (
    SELECT
        MAX(CASE WHEN rn = 1 THEN uploaded_from_source END) AS today_dt,
        MAX(CASE WHEN rn = 2 THEN uploaded_from_source END) AS yday_dt
    FROM (
        SELECT
            uploaded_from_source,
            DENSE_RANK() OVER (ORDER BY uploaded_from_source DESC) AS rn
        FROM dates
    )
),
today AS (
    SELECT *
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL
    WHERE site = '{site}' and planning_point='{planningPoint}'
        AND uploaded_from_source = (SELECT today_dt FROM picked)
),
yday AS (
    SELECT *
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL
    WHERE site = '{site}' and planning_point='{planningPoint}'
        AND uploaded_from_source = (SELECT yday_dt FROM picked)
),
diffs AS (
    SELECT
        COALESCE(td.site, yd.site) AS site,
        COALESCE(td.planning_point, yd.planning_point) AS planning_point,
        COALESCE(td.part_number, yd.part_number) AS part_number,
        COALESCE(td.mfg_supplier_code, yd.mfg_supplier_code) AS mfg_supplier_code,

        (SELECT yday_dt FROM picked)  AS yesterday_date,
        (SELECT today_dt FROM picked) AS today_date,

        yd.safety_stock_lead_time        AS y_safety_stock_lead_time,
        td.safety_stock_lead_time        AS t_safety_stock_lead_time,
        yd.safety_stock_nr_of_parts      AS y_safety_stock_nr_of_parts,
        td.safety_stock_nr_of_parts      AS t_safety_stock_nr_of_parts,
        yd.fls_yard_leadtime_minutes     AS y_fls_yard_leadtime_minutes,
        td.fls_yard_leadtime_minutes     AS t_fls_yard_leadtime_minutes,
        yd.fls_yard_leadtime_shifts_calc AS y_fls_yard_leadtime_shifts_calc,
        td.fls_yard_leadtime_shifts_calc AS t_fls_yard_leadtime_shifts_calc,

        IFF(
                 NVL(td.safety_stock_lead_time, -999999)        <> NVL(yd.safety_stock_lead_time, -999999)
            OR NVL(td.safety_stock_nr_of_parts, -999999)      <> NVL(yd.safety_stock_nr_of_parts, -999999)
            OR NVL(td.fls_yard_leadtime_minutes, -999999)     <> NVL(yd.fls_yard_leadtime_minutes, -999999)
            OR NVL(td.fls_yard_leadtime_shifts_calc, -999999) <> NVL(yd.fls_yard_leadtime_shifts_calc, -999999),
            1, 0
        ) AS changed_flag
    FROM yday yd
    FULL OUTER JOIN today td
        ON td.site = yd.site
     AND td.planning_point = yd.planning_point
     AND td.part_number = yd.part_number
     AND td.mfg_supplier_code = yd.mfg_supplier_code
)
SELECT
    *,
    SUM(changed_flag) OVER () AS num_changed_rows
FROM diffs
WHERE changed_flag = 1;";

                var dt = await _snowflake.QueryAsync(sql);
                var changes = new List<SafetyStockChange>();

                foreach (DataRow row in dt.Rows)
                {
                        var change = new SafetyStockChange
                        {
                                Site = row.Field<string>("SITE"),
                                PlanningPoint = row.Field<string>("PLANNING_POINT"),
                                PartNumber = row.Field<string>("PART_NUMBER"),
                                MfgSupplierCode = row.Field<string>("MFG_SUPPLIER_CODE"),

                                YesterdayDate = row.Field<DateTime?>("YESTERDAY_DATE"),
                                TodayDate = row.Field<DateTime?>("TODAY_DATE"),

                                YSafetyStockLeadTime = row.Field<decimal?>("Y_SAFETY_STOCK_LEAD_TIME"),
                                TSafetyStockLeadTime = row.Field<decimal?>("T_SAFETY_STOCK_LEAD_TIME"),
                                YSafetyStockNrOfParts = row.Field<decimal?>("Y_SAFETY_STOCK_NR_OF_PARTS"),
                                TSafetyStockNrOfParts = row.Field<decimal?>("T_SAFETY_STOCK_NR_OF_PARTS"),
                                YFlsYardLeadtimeMinutes = row.Field<decimal?>("Y_FLS_YARD_LEADTIME_MINUTES"),
                                TFlsYardLeadtimeMinutes = row.Field<decimal?>("T_FLS_YARD_LEADTIME_MINUTES"),
                                YFlsYardLeadtimeShiftsCalc = row.Field<decimal?>("Y_FLS_YARD_LEADTIME_SHIFTS_CALC"),
                                TFlsYardLeadtimeShiftsCalc = row.Field<decimal?>("T_FLS_YARD_LEADTIME_SHIFTS_CALC"),

                                ChangedFlag = Convert.ToInt32(row["CHANGED_FLAG"]),
                                NumChangedRows = Convert.ToInt32(row["NUM_CHANGED_ROWS"]) 
                        };
                        changes.Add(change);
                }

                return changes;
        }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Run lightweight query to validate connectivity
            var dt = await _snowflake.QueryAsync("SELECT SHP_SUPPLIER_CODE FROM MANUFACTURING_BUSINESS_DATA_PRODUCTS.INVENTORY_BY_PURPOSE.DEMAND_FLUCTUATION_INPUT_INV_MATRIX LIMIT 100");
            return dt.Rows.Count > 0;
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