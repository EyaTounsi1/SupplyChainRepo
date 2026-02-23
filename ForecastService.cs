using PartTracker.Models;
using PartTracker.Shared.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PartTracker;

public class ForecastService : IForecastService
{
    private readonly SnowflakeService _snowflakeService;

    public ForecastService(SnowflakeService snowflakeService)
    {
        _snowflakeService = snowflakeService;
    }

    /// <summary>
    /// Returns forecast for ALL parts/sites (POC -> production). Be careful: can be huge.
    /// </summary>
    public Task<List<ForecastItem>> GetForecastDataAsync()
        => GetForecastDataAsync(siteFilter: null, partNumberFilter: null, mfgCodeFilter: null, months: 3);

    /// <summary>
    /// Optional filters you can use for testing/performance. Pass null for "all".
    /// </summary>
    public async Task<List<ForecastItem>> GetForecastDataAsync(string? siteFilter, string? partNumberFilter, string? mfgCodeFilter, int months = 3)
    {
        // Build SQL with optional filters pushed as early as possible.
        // NOTE: Prefer parameterized queries in SnowflakeService if you can.
        // This uses safe literal escaping to avoid breaking SQL when values contain apostrophes.
        var siteWhere = string.IsNullOrWhiteSpace(siteFilter)
            ? ""
            : $" AND {{0}}.SITE = '{EscapeSqlLiteral(siteFilter.Trim())}'";

        var partWhere = string.IsNullOrWhiteSpace(partNumberFilter)
            ? ""
            : $" AND {{0}}.PART_NUMBER = '{EscapeSqlLiteral(partNumberFilter.Trim())}'";

        // Filters applied to source tables early (pis/ds/pd/ss) AND again at the end as a safety net.
        string Sql = $@"
WITH
-- ============================================================================
-- STEP 1: STARTING WIP EVENTS (starting balance from inventory)
-- ============================================================================
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

-- ============================================================================
-- STEP 2: CURRENT GIT EVENTS (Pickup + Arrival, current in-transit)
-- ============================================================================
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

    -- Arrival
    SELECT
        ds.SITE AS site,
        ds.PART_NUMBER AS part_number,
        ds.ARRIVAL_TIME_EARLIEST::DATE AS event_date,
        'Arrival' AS event_type,
        ds.PART_AMOUNT AS quantity,
        -ds.PART_AMOUNT AS git_impact,
        ds.PART_AMOUNT AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
    WHERE ds.DEPARTURE_TIME_EARLIEST::DATE <= CURRENT_DATE()
      AND ds.ARRIVAL_TIME_EARLIEST::DATE > CURRENT_DATE()
    {string.Format(siteWhere, "ds")}
    {string.Format(partWhere, "ds")}
),

-- ============================================================================
-- STEP 3: PICKUP PLAN EVENTS (future pickups/arrivals)
-- ============================================================================
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

    -- Arrival
    SELECT
        ds.SITE AS site,
        ds.PART_NUMBER AS part_number,
        ds.ARRIVAL_TIME_EARLIEST::DATE AS event_date,
        'Arrival' AS event_type,
        ds.PART_AMOUNT AS quantity,
        -ds.PART_AMOUNT AS git_impact,
        ds.PART_AMOUNT AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
    WHERE ds.DEPARTURE_TIME_EARLIEST::DATE > CURRENT_DATE()
    {string.Format(siteWhere, "ds")}
    {string.Format(partWhere, "ds")}
),

-- ============================================================================
-- STEP 4: CONSUMPTION EVENTS (future demand)
-- ============================================================================
consumption_events AS (
    SELECT
        pd.SITE AS site,
        pd.PART_NUMBER AS part_number,
        pd.PRODUCTION_DAY AS event_date,
        'Consumption' AS event_type,
        SUM(pd.PART_AMOUNT) AS quantity,
        0 AS git_impact,
        -SUM(pd.PART_AMOUNT) AS wip_impact,
        NULL::NUMBER AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_DEMAND_AS_MANUFACTURED.PART_DEMAND_AS_MANUFACTURED_CURRENT_DATE pd
    WHERE pd.PRODUCTION_DAY > CURRENT_DATE()
    {string.Format(siteWhere, "pd")}
    {string.Format(partWhere, "pd")}
    GROUP BY pd.SITE, pd.PART_NUMBER, pd.PRODUCTION_DAY
),

-- ============================================================================
-- STEP 5: UNION ALL EVENTS
-- ============================================================================
all_events AS (
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM starting_wip_events
    UNION ALL
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM current_git_events
    UNION ALL
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM pickup_plan_events
    UNION ALL
    SELECT site, part_number, event_date, event_type, quantity, git_impact, wip_impact, price FROM consumption_events
),

-- ============================================================================
-- STEP 5.5: FILTER PARTS (must have supply AND consumption)
-- ============================================================================
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

-- ============================================================================
-- STEP 6: ADD PRICES (single source of truth -> all values derived from this)
-- ============================================================================
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

-- ============================================================================
-- STEP 7: RUNNING BALANCES
-- ============================================================================
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

-- ============================================================================
-- STEP 9: DAILY TIMELINE (variable months)
-- ============================================================================
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

    ss.SAFETY_STOCK_NR_OF_PARTS AS safety_stock_nr_of_parts,
    ss.SAFETY_STOCK_LEAD_TIME AS safety_stock_lead_time,

    dt.wip_balance - ss.SAFETY_STOCK_NR_OF_PARTS AS wip_minus_ss,

    CASE
        WHEN dt.wip_balance <= 0 THEN 0
        WHEN du.avg_daily_usage > 0 THEN dt.wip_balance / du.avg_daily_usage
        ELSE NULL
    END AS days_until_stockout,

    CASE
        WHEN dt.wip_balance < ss.SAFETY_STOCK_NR_OF_PARTS
        THEN (ss.SAFETY_STOCK_NR_OF_PARTS - dt.wip_balance) * dt.price
        ELSE 0
    END AS capital_at_risk_sek,

    CASE
        WHEN dt.wip_balance < ss.SAFETY_STOCK_NR_OF_PARTS THEN 'Below SS'
        WHEN dt.wip_balance = ss.SAFETY_STOCK_NR_OF_PARTS THEN 'At SS'
        ELSE 'Above SS'
    END AS ss_deviation_flag

FROM daily_timeline dt
LEFT JOIN ss
    ON dt.site = ss.SITE AND dt.part_number = ss.PART_NUMBER
LEFT JOIN daily_usage du
    ON dt.site = du.site AND dt.part_number = du.part_number
WHERE 1=1 and dt.site='VCT'
{(string.IsNullOrWhiteSpace(siteFilter) ? "" : $" AND dt.site = '{EscapeSqlLiteral(siteFilter!.Trim())}'")}
{(string.IsNullOrWhiteSpace(partNumberFilter) ? "" : $" AND dt.part_number = '{EscapeSqlLiteral(partNumberFilter!.Trim())}'")}{(string.IsNullOrWhiteSpace(mfgCodeFilter) ? "" : $" AND ss.MFG_SUPPLIER_CODE = '{EscapeSqlLiteral(mfgCodeFilter!.Trim())}'")}ORDER BY dt.site, dt.part_number, dt.date
limit 100;
";

        // One Snowflake round-trip only
        DataTable dataTable = await _snowflakeService.QueryAsync(Sql);

        // Convert to List<ForecastItem>
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

    private static string? GetString(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col]?.ToString() : null;

    private static DateTime GetDateTime(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDateTime(row[col]) : DateTime.MinValue;

    private static decimal GetDecimal(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDecimal(row[col]) : 0m;

    private static decimal? GetNullableDecimal(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDecimal(row[col]) : null;
}