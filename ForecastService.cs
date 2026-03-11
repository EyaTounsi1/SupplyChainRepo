using PartTracker.Models;
using PartTracker.Shared.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;

namespace PartTracker;

public class ForecastService : IForecastService
{
    private readonly SnowflakeService _snowflakeService;

    public ForecastService(SnowflakeService snowflakeService)
    {
        _snowflakeService = snowflakeService;
    }

    public Task<List<ForecastItem>> GetForecastDataAsync()
        => GetForecastDataAsync(siteFilter: null, partNumberFilter: null, mfgCodeFilter: null, months: 3);

    public async Task<List<ForecastItem>> GetForecastDataAsync(
        string? siteFilter,
        string? partNumberFilter,
        string? mfgCodeFilter,
        int months = 3,
        double demandMultiplier = 1.0,
        int leadTimeShiftDays = 0,
        double? ssOverrideParts = null,
        double? ssOverrideLeadTime = null)
    {
        // Basic validation (prevents weird inputs / accidental huge queries)
        if (months < 1) months = 1;
        if (months > 24) months = 24; // clamp - adjust as you like
        if (demandMultiplier <= 0) demandMultiplier = 1.0;
        if (leadTimeShiftDays < -365) leadTimeShiftDays = -365;
        if (leadTimeShiftDays > 365) leadTimeShiftDays = 365;

        var siteWhere = string.IsNullOrWhiteSpace(siteFilter)
            ? ""
            : $" AND {{0}}.SITE = '{EscapeSqlLiteral(siteFilter.Trim())}'";

        var partWhere = string.IsNullOrWhiteSpace(partNumberFilter)
            ? ""
            : $" AND {{0}}.PART_NUMBER = '{EscapeSqlLiteral(partNumberFilter.Trim())}'";

        // NOTE:
        // - We format numeric values using InvariantCulture to avoid 1,1 vs 1.1 issues.
        // - We inject only numbers (validated above), and we still escape string filters.
        string sql = $@"
WITH
params AS (
    SELECT
        {ToSqlNumber(demandMultiplier)} AS demand_multiplier,
        {leadTimeShiftDays} AS lead_time_shift_days,
        {ToSqlNullableNumber(ssOverrideParts)} AS ss_override_parts,
        {ToSqlNullableNumber(ssOverrideLeadTime)} AS ss_override_lead_time
),

starting_wip_events AS (
    SELECT
        pis.SITE AS site,
        pis.PART_NUMBER AS part_number,
        CURRENT_DATE()::DATE AS event_date,
        'Starting Balance' AS event_type,
        pis.AVAILABLE_INVENTORY AS quantity,
        0 AS git_impact,
        pis.AVAILABLE_INVENTORY AS wip_impact,
        pis.STANDARD_PRICE AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_INVENTORY_IN_STOCK_AS_MANUFACTURED.PART_INVENTORY_IN_STOCK_AS_MANUFACTURED pis
    WHERE pis.PRODUCTION_DAY =
        CASE
            WHEN pis.SITE = 'VCCH' THEN CURRENT_DATE() - 1
            ELSE CURRENT_DATE()
        END
    {string.Format(siteWhere, "pis")}
    {string.Format(partWhere, "pis")}
),

current_git_events AS (
    -- Pickup
    SELECT
        ds.SITE AS site,
        ds.PART_NUMBER AS part_number,
        ds.DEPARTURE_TIME_EARLIEST::DATE AS event_date,
        'Pickup' AS event_type,
        ds.PART_AMOUNT AS quantity,
        ds.PART_AMOUNT AS git_impact,
        0 AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
    WHERE ds.DEPARTURE_TIME_EARLIEST::DATE <= CURRENT_DATE()
      AND ds.ARRIVAL_TIME_EARLIEST::DATE > CURRENT_DATE()
    {string.Format(siteWhere, "ds")}
    {string.Format(partWhere, "ds")}

    UNION ALL

    -- Arrival (shift by lead_time_shift_days)
    SELECT
        ds.SITE AS site,
        ds.PART_NUMBER AS part_number,
        DATEADD(day, p.lead_time_shift_days, ds.ARRIVAL_TIME_EARLIEST::DATE) AS event_date,
        'Arrival' AS event_type,
        ds.PART_AMOUNT AS quantity,
        -ds.PART_AMOUNT AS git_impact,
        ds.PART_AMOUNT AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
    CROSS JOIN params p
    WHERE ds.DEPARTURE_TIME_EARLIEST::DATE <= CURRENT_DATE()
      AND ds.ARRIVAL_TIME_EARLIEST::DATE > CURRENT_DATE()
    {string.Format(siteWhere, "ds")}
    {string.Format(partWhere, "ds")}
),

pickup_plan_events AS (
    -- Pickup
    SELECT
        ds.SITE AS site,
        ds.PART_NUMBER AS part_number,
        ds.DEPARTURE_TIME_EARLIEST::DATE AS event_date,
        'Pickup' AS event_type,
        ds.PART_AMOUNT AS quantity,
        ds.PART_AMOUNT AS git_impact,
        0 AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
    WHERE ds.DEPARTURE_TIME_EARLIEST::DATE > CURRENT_DATE()
    {string.Format(siteWhere, "ds")}
    {string.Format(partWhere, "ds")}

    UNION ALL

    -- Arrival (shift by lead_time_shift_days)
    SELECT
        ds.SITE AS site,
        ds.PART_NUMBER AS part_number,
        DATEADD(day, p.lead_time_shift_days, ds.ARRIVAL_TIME_EARLIEST::DATE) AS event_date,
        'Arrival' AS event_type,
        ds.PART_AMOUNT AS quantity,
        -ds.PART_AMOUNT AS git_impact,
        ds.PART_AMOUNT AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
    CROSS JOIN params p
    WHERE ds.DEPARTURE_TIME_EARLIEST::DATE > CURRENT_DATE()
    {string.Format(siteWhere, "ds")}
    {string.Format(partWhere, "ds")}
),

consumption_events AS (
    SELECT
        pd.SITE AS site,
        pd.PART_NUMBER AS part_number,
        pd.PRODUCTION_DAY AS event_date,
        'Consumption' AS event_type,
        SUM(pd.PART_AMOUNT) * p.demand_multiplier AS quantity,
        0 AS git_impact,
        -(SUM(pd.PART_AMOUNT) * p.demand_multiplier) AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_DEMAND_AS_MANUFACTURED.PART_DEMAND_AS_MANUFACTURED_CURRENT_DATE pd
    CROSS JOIN params p
    WHERE pd.PRODUCTION_DAY > CURRENT_DATE()
    {string.Format(siteWhere, "pd")}
    {string.Format(partWhere, "pd")}
    GROUP BY pd.SITE, pd.PART_NUMBER, pd.PRODUCTION_DAY, p.demand_multiplier
),

all_events AS (
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM starting_wip_events
    UNION ALL
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM current_git_events
    UNION ALL
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM pickup_plan_events
    UNION ALL
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM consumption_events
),

parts_with_supply AS (
    SELECT DISTINCT site, part_number
    FROM all_events
    WHERE event_type IN ('Pickup', 'Arrival')
),
parts_with_consumption AS (
    SELECT DISTINCT site, part_number
    FROM all_events
    WHERE event_type = 'Consumption'
),
valid_parts AS (
    SELECT site, part_number FROM parts_with_supply
    INTERSECT
    SELECT site, part_number FROM parts_with_consumption
),
all_events_filtered AS (
    SELECT e.*
    FROM all_events e
    INNER JOIN valid_parts vp
        ON e.site = vp.site AND e.part_number = vp.part_number
),

events_with_prices AS (
    SELECT
        e.site,
        e.part_number,
        e.event_date,
        e.event_type,
        e.quantity,
        e.git_impact,
        e.wip_impact,
        COALESCE(e.price, pim.STANDARD_PRICE, 0) AS price
    FROM all_events_filtered e
    LEFT JOIN MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_INFORMATION_AS_MANUFACTURED.PART_INFORMATION_AS_MANUFACTURED pim
        ON pim.SITE = e.site
       AND pim.PART_NUMBER = e.part_number
),

events_with_balances AS (
    SELECT
        *,
        SUM(git_impact) OVER (
            PARTITION BY site, part_number
            ORDER BY event_date,
                     CASE event_type
                         WHEN 'Starting Balance' THEN 1
                         WHEN 'Pickup' THEN 2
                         WHEN 'Arrival' THEN 3
                         WHEN 'Consumption' THEN 4
                     END
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS git_balance,
        SUM(wip_impact) OVER (
            PARTITION BY site, part_number
            ORDER BY event_date,
                     CASE event_type
                         WHEN 'Starting Balance' THEN 1
                         WHEN 'Pickup' THEN 2
                         WHEN 'Arrival' THEN 3
                         WHEN 'Consumption' THEN 4
                     END
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS wip_balance
    FROM events_with_prices
),

final_events AS (
    SELECT
        site,
        part_number,
        event_date,
        event_type,
        quantity,
        git_impact,
        wip_impact,
        price,
        git_balance,
        wip_balance
    FROM events_with_balances
),

calendar AS (
    SELECT DATEADD(day, SEQ4(), CURRENT_DATE()) AS calendar_date
    FROM TABLE(GENERATOR(ROWCOUNT => {months * 31}))
    WHERE DATEADD(day, SEQ4(), CURRENT_DATE()) <= DATEADD(month, {months}, CURRENT_DATE())
),
parts AS (
    SELECT DISTINCT site, part_number FROM final_events
),
part_dates AS (
    SELECT p.site, p.part_number, c.calendar_date
    FROM parts p
    CROSS JOIN calendar c
),
part_date_last_event AS (
    SELECT
        pd.site,
        pd.part_number,
        pd.calendar_date,
        fe.git_balance,
        fe.wip_balance,
        fe.price,
        ROW_NUMBER() OVER (
            PARTITION BY pd.site, pd.part_number, pd.calendar_date
            ORDER BY fe.event_date DESC,
                     CASE fe.event_type
                         WHEN 'Starting Balance' THEN 1
                         WHEN 'Arrival' THEN 2
                         WHEN 'Pickup' THEN 3
                         WHEN 'Consumption' THEN 4
                     END DESC
        ) AS rn
    FROM part_dates pd
    LEFT JOIN final_events fe
        ON fe.site = pd.site
       AND fe.part_number = pd.part_number
       AND fe.event_date <= pd.calendar_date
),
last_state_per_part_date AS (
    SELECT
        site,
        part_number,
        calendar_date,
        COALESCE(git_balance, 0) AS git_balance,
        COALESCE(wip_balance, 0) AS wip_balance,
        COALESCE(price, 0) AS price
    FROM part_date_last_event
    WHERE rn = 1
),
daily_timeline AS (
    SELECT
        site,
        part_number,
        calendar_date AS date,
        git_balance,
        wip_balance,
        price,
        git_balance * price AS git_value_sek,
        wip_balance * price AS wip_value_sek,
        ROUND(git_balance * price / 1000000.0, 2) AS git_value_m_sek,
        ROUND(wip_balance * price / 1000000.0, 2) AS wip_value_m_sek,
        ROUND((git_balance * price + wip_balance * price) / 1000000.0, 2) AS total_capital_m_sek
    FROM last_state_per_part_date
),
daily_usage AS (
    SELECT
        pd.site,
        pd.part_number,
        SUM(pd.part_amount) / NULLIF(COUNT(DISTINCT pd.production_day), 0) AS avg_daily_usage
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_DEMAND_AS_MANUFACTURED.PART_DEMAND_AS_MANUFACTURED_CURRENT_DATE pd
    WHERE pd.PRODUCTION_DAY >= DATEADD(day, -30, CURRENT_DATE())
    {string.Format(siteWhere, "pd")}
    {string.Format(partWhere, "pd")}
    GROUP BY pd.site, pd.part_number
),
ss AS (
    SELECT
        SITE,
        PART_NUMBER,
        MFG_SUPPLIER_CODE,
        SAFETY_STOCK_NR_OF_PARTS,
        SAFETY_STOCK_LEAD_TIME
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED
    WHERE 1=1
    {string.Format(siteWhere, "MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED")}
    {string.Format(partWhere, "MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED")}
)

SELECT
    dt.site,
    dt.part_number,
    dt.date,
    dt.git_balance,
    dt.wip_balance,
    dt.price,
    dt.git_value_sek,
    dt.wip_value_sek,
    dt.git_value_m_sek,
    dt.wip_value_m_sek,
    dt.total_capital_m_sek,
    ss.MFG_SUPPLIER_CODE,

    COALESCE(p.ss_override_parts, ss.SAFETY_STOCK_NR_OF_PARTS) AS safety_stock_nr_of_parts,
    COALESCE(p.ss_override_lead_time, ss.SAFETY_STOCK_LEAD_TIME) AS safety_stock_lead_time,

    dt.wip_balance - COALESCE(p.ss_override_parts, ss.SAFETY_STOCK_NR_OF_PARTS) AS wip_minus_ss,

    CASE
        WHEN dt.wip_balance <= 0 THEN 0
        WHEN du.avg_daily_usage > 0 THEN dt.wip_balance / du.avg_daily_usage
        ELSE NULL
    END AS days_until_stockout,

    CASE
        WHEN dt.wip_balance < COALESCE(p.ss_override_parts, ss.SAFETY_STOCK_NR_OF_PARTS)
        THEN (COALESCE(p.ss_override_parts, ss.SAFETY_STOCK_NR_OF_PARTS) - dt.wip_balance) * dt.price
        ELSE 0
    END AS capital_at_risk_sek,

    CASE
        WHEN dt.wip_balance < COALESCE(p.ss_override_parts, ss.SAFETY_STOCK_NR_OF_PARTS) THEN 'Below SS'
        WHEN dt.wip_balance = COALESCE(p.ss_override_parts, ss.SAFETY_STOCK_NR_OF_PARTS) THEN 'At SS'
        ELSE 'Above SS'
    END AS ss_deviation_flag

FROM daily_timeline dt
LEFT JOIN ss
    ON dt.site = ss.SITE AND dt.part_number = ss.PART_NUMBER
LEFT JOIN daily_usage du
    ON dt.site = du.site AND dt.part_number = du.part_number
CROSS JOIN params p
WHERE 1=1 and site='VCT'
{(string.IsNullOrWhiteSpace(siteFilter) ? "" : $" AND dt.site = '{EscapeSqlLiteral(siteFilter!.Trim())}'")}
{(string.IsNullOrWhiteSpace(partNumberFilter) ? "" : $" AND dt.part_number = '{EscapeSqlLiteral(partNumberFilter!.Trim())}'")}
{(string.IsNullOrWhiteSpace(mfgCodeFilter) ? "" : $" AND ss.MFG_SUPPLIER_CODE = '{EscapeSqlLiteral(mfgCodeFilter!.Trim())}'")}
ORDER BY dt.site, dt.part_number, dt.date
;
";

        DataTable dataTable = await _snowflakeService.QueryAsync(sql, "ManufacturingEnterpriseDataProducts");

        var items = new List<ForecastItem>(capacity: dataTable.Rows.Count);

        foreach (DataRow row in dataTable.Rows)
        {
            items.Add(new ForecastItem
            {
                Site = GetString(row, "site"),
                Part_Number = GetString(row, "part_number"),
                Date = GetDateTime(row, "date"),

                Git_Balance = GetDecimal(row, "git_balance"),
                Wip_Balance = GetDecimal(row, "wip_balance"),
                Price = GetDecimal(row, "price"),

                Git_Value_Sek = GetDecimal(row, "git_value_sek"),
                Wip_Value_Sek = GetDecimal(row, "wip_value_sek"),
                Git_Value_M_Sek = GetDecimal(row, "git_value_m_sek"),
                Wip_Value_M_Sek = GetDecimal(row, "wip_value_m_sek"),
                Total_Capital_M_Sek = GetDecimal(row, "total_capital_m_sek"),

                Mfg_Supplier_Code = GetString(row, "MFG_SUPPLIER_CODE"),

                Safety_Stock_Nr_Of_Parts = GetNullableDecimal(row, "safety_stock_nr_of_parts"),
                Safety_Stock_Lead_Time = GetNullableDecimal(row, "safety_stock_lead_time"),
                Wip_Minus_Ss = GetNullableDecimal(row, "wip_minus_ss"),
                Days_Until_Stockout = GetNullableDecimal(row, "days_until_stockout"),
                Capital_At_Risk_Sek = GetNullableDecimal(row, "capital_at_risk_sek"),
                Ss_Deviation_Flag = GetString(row, "ss_deviation_flag")
            });
        }

        return items;
    }

    // -------- Helpers --------

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

    private static string ToSqlNumber(double value)
        => value.ToString("0.################", CultureInfo.InvariantCulture);

    private static string ToSqlNullableNumber(double? value)
        => value.HasValue ? ToSqlNumber(value.Value) : "NULL";

    private static string? GetString(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col]?.ToString() : null;

    private static DateTime GetDateTime(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDateTime(row[col]) : DateTime.MinValue;

    private static decimal GetDecimal(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDecimal(row[col]) : 0m;

    private static decimal? GetNullableDecimal(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDecimal(row[col]) : null;
}