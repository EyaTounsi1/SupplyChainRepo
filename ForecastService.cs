using Microsoft.EntityFrameworkCore;
using PartTracker.Models;
using PartTracker.Shared.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;

namespace PartTracker;

public class ForecastService : IForecastService
{
    private readonly AppDbContext _db;
    private readonly AutomationDbContext _automationDb;
    private readonly SnowflakeService _snowflakeService;

    public ForecastService(AppDbContext db, AutomationDbContext automationDb, SnowflakeService snowflakeService)
    {
        _db = db;
        _automationDb = automationDb;
        _snowflakeService = snowflakeService;
    }

    public async Task<List<ForecastItem>> GetForecastDataAsync()
    {
        // Load prices from MySQL automation database
        var priceMap = await _automationDb.PartPrices
            .ToDictionaryAsync(p => p.PartNumber, p => p.StandardPrice);

        // Replace this SQL with your actual MySQL forecast script
        var sql = @"
--===========================================================================
-- PURPOSE: Generate a detailed daily timeline of inventory events (GIT and WIP)
-- STEP 1: CREATE STARTING WIP EVENTS (with prices from inventory table)
-- ============================================================================
WITH starting_wip_events AS (
    SELECT
        pis.SITE AS site,
        pis.PART_NUMBER AS part_number,
        CURRENT_DATE()::DATE AS event_date,
        'Starting Balance' AS event_type,
        pis.AVAILABLE_INVENTORY AS quantity,
        0 AS git_impact,
        pis.AVAILABLE_INVENTORY AS wip_impact,
        (pis.AVAILABLE_INVENTORY * pis.STANDARD_PRICE) / 1000000.0 AS wip_value_m_sek,
        pis.STANDARD_PRICE AS price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_INVENTORY_IN_STOCK_AS_MANUFACTURED.PART_INVENTORY_IN_STOCK_AS_MANUFACTURED pis
    --- charleston site data is as of previous day
      WHERE pis.PRODUCTION_DAY =
        CASE
            WHEN pis.SITE = 'VCCH' THEN CURRENT_DATE() - 1
            ELSE CURRENT_DATE()
        END
),
-- ============================================================================
-- STEP 2: CREATE CURRENT GIT EVENTS (Pickup + Arrival)
-- Split by data source: VCG vs Non-VCG
-- ============================================================================
-- Step 2A: VCG site data from MANUFACTURING schema
current_git_events AS (
    -- Pickup events
    SELECT
        ds.SITE AS site,
        ds._ID_DELIVERY_SCHEDULE_AS_MANUFACTURED,
        ds.PART_NUMBER AS part_number,
        ds.DEPARTURE_TIME_EARLIEST::DATE AS event_date,
        'Pickup' AS event_type,
        ds.PART_AMOUNT AS quantity,
        ds.PART_AMOUNT AS git_impact,
        0 AS wip_impact,
        NULL as price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
      WHERE ds.DEPARTURE_TIME_EARLIEST::DATE <= CURRENT_DATE()
      AND ds.ARRIVAL_TIME_EARLIEST::DATE > CURRENT_DATE()

    UNION ALL

    -- Arrival events
    SELECT
        ds.site AS site,
        ds._ID_DELIVERY_SCHEDULE_AS_MANUFACTURED,
        ds.PART_NUMBER AS part_number,
        ds.ARRIVAL_TIME_EARLIEST::DATE AS event_date,
        'Arrival' AS event_type,
        ds.PART_AMOUNT AS quantity,
        -ds.PART_AMOUNT AS git_impact,
        ds.PART_AMOUNT AS wip_impact,
        NULL as price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
      WHERE ds.DEPARTURE_TIME_EARLIEST::DATE <= CURRENT_DATE()
      AND ds.ARRIVAL_TIME_EARLIEST::DATE > CURRENT_DATE()
),

-- ============================================================================
-- STEP 3: CREATE PICKUP PLAN EVENTS (Future Pickup + Arrival)
-- Split by data source: VCG vs Non-VCG
-- ============================================================================
pickup_plan_events AS (
    -- Pickup events
    SELECT
        ds.site AS site,
        ds._ID_DELIVERY_SCHEDULE_AS_MANUFACTURED,
        ds.PART_NUMBER AS part_number,
        ds.DEPARTURE_TIME_EARLIEST::DATE AS event_date,
        'Pickup' AS event_type,
        ds.PART_AMOUNT AS quantity,
        ds.PART_AMOUNT AS git_impact,
        0 AS wip_impact,
        NULL as price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
      WHERE ds.DEPARTURE_TIME_EARLIEST::DATE > CURRENT_DATE()

    UNION ALL

    -- Arrival events
    SELECT
        ds.site AS site,
        ds._ID_DELIVERY_SCHEDULE_AS_MANUFACTURED,
        ds.PART_NUMBER AS part_number,
        ds.ARRIVAL_TIME_EARLIEST::DATE AS event_date,
        'Arrival' AS event_type,
        ds.PART_AMOUNT AS quantity,
        -ds.PART_AMOUNT AS git_impact,
        ds.PART_AMOUNT AS wip_impact,
        NULL as price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.DELIVERY_SCHEDULE_AS_MANUFACTURED.DELIVERY_SCHEDULE_AS_MANUFACTURED ds
      WHERE ds.DEPARTURE_TIME_EARLIEST::DATE > CURRENT_DATE()
),


-- ============================================================================
-- STEP 4: CREATE CONSUMPTION EVENTS
-- ============================================================================
consumption_events AS (
    SELECT
        pd.site AS site,
        pd.PART_NUMBER AS part_number,
        -- Calculate week ending date (Sunday)
         pd.PRODUCTION_DAY as event_date,
        'Consumption' AS event_type,
        SUM(pd.PART_AMOUNT) AS quantity,
        0 AS git_impact,
        -SUM(pd.PART_AMOUNT) AS wip_impact,
        NULL as price
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_DEMAND_AS_MANUFACTURED.PART_DEMAND_AS_MANUFACTURED_CURRENT_DATE pd
      WHERE pd.PRODUCTION_DAY > CURRENT_DATE()
    GROUP BY
        pd.site,
        pd.PART_NUMBER,
        pd.PRODUCTION_DAY
),

-- ============================================================================
-- STEP 5: UNION ALL EVENTS (ensure all have same 7 columns)
-- ============================================================================
all_events AS (
    SELECT
        site,
        part_number,
        event_date,
        event_type,
        quantity,
        git_impact,
        wip_impact,
        price --this has value
    FROM starting_wip_events

    UNION ALL

    SELECT
        site,
        part_number,
        event_date,
        event_type,
        quantity,
        git_impact,
        wip_impact,
        price --this is NULL for now
    FROM current_git_events

    UNION ALL

    SELECT
        site,
        part_number,
        event_date,
        event_type,
        quantity,
        git_impact,
        wip_impact,
        price --this is NULL for now
    FROM pickup_plan_events

    UNION ALL

    SELECT
        site,
        part_number,
        event_date,
        event_type,
        quantity,
        git_impact,
        wip_impact,
        price --this is NULL for now
    FROM consumption_events
),

-- select * from all_events,

-- ============================================================================
-- STEP 5.5:
-- a. There can be parts where there are consumption events and no pickup/ arrival
-- b. There can be parts where there are no pickup/ arrival events and no consumption
-- THESE PART NUMBERS NEED TO BE FILTERED
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
    SELECT site, part_number
    FROM parts_with_supply

    INTERSECT

    SELECT site, part_number
    FROM parts_with_consumption
),

-- Get event counts for ALL parts
part_event_counts AS (
    SELECT
        site,
        part_number,
        COUNT(CASE WHEN event_type IN ('Pickup', 'Arrival') THEN 1 END) AS supply_event_count,
        COUNT(CASE WHEN event_type = 'Consumption' THEN 1 END) AS consumption_event_count
    FROM all_events
    GROUP BY site, part_number
),

-- Invalid parts: Both types
invalid_parts AS (
    SELECT
        pec.site,
        pec.part_number,
        CASE
            WHEN pec.supply_event_count > 0 AND pec.consumption_event_count = 0
                THEN 'Has Supply but NO Consumption'
            WHEN pec.supply_event_count = 0 AND pec.consumption_event_count > 0
                THEN 'Has Consumption but NO Supply'
            ELSE 'Unknown Issue'  -- Should not happen, but safety check
        END AS issue_type,
        pec.supply_event_count,
        pec.consumption_event_count
    FROM part_event_counts pec
    LEFT JOIN valid_parts vp
        ON pec.site = vp.site AND pec.part_number = vp.part_number
    WHERE vp.part_number IS NULL
),
all_events_filtered AS (
    SELECT e.*
    FROM all_events e
    INNER JOIN valid_parts vp
        ON e.site = vp.site AND e.part_number = vp.part_number
),


-- ============================================================================
-- STEP 6: ADD PRICES (only for events that don't have them yet)
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
        -- Use price from event if present (Starting Balance), otherwise from part info
        COALESCE(e.price, pi.STANDARD_PRICE, 0) AS price
    FROM all_events_filtered  e
    LEFT JOIN MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_INFORMATION_AS_MANUFACTURED.PART_INFORMATION_AS_MANUFACTURED pi
        ON e.part_number = pi.PART_NUMBER
        AND e.site = pi.SITE
        AND e.price IS NULL
),

-- ============================================================================
-- STEP 7: CALCULATE RUNNING BALANCES (Cumulative Sum)
-- ============================================================================
events_with_balances AS (
    SELECT
        *,
        -- Running sum with proper event ordering
        SUM(git_impact) OVER (
            PARTITION BY site, part_number
            ORDER BY event_date,
                     CASE event_type
                         WHEN 'Starting Balance' THEN 1  -- Process first
                         WHEN 'Pickup' THEN 2             -- Then pickups
                         WHEN 'Arrival' THEN 3           -- Then arrivals
                         WHEN 'Consumption' THEN 4        -- Process last
                     END
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS git_balance,

        SUM(wip_impact) OVER (
            PARTITION BY site, part_number
            ORDER BY event_date,
                     CASE event_type
                         WHEN 'Starting Balance' THEN 1  -- Process first
                         WHEN 'Pickup' THEN 2             -- Then pickups
                         WHEN 'Arrival' THEN 3           -- Then arrivals
                         WHEN 'Consumption' THEN 4        -- Process last
                     END
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS wip_balance
    FROM events_with_prices
),
-- ============================================================================
-- STEP 8: CALCULATE VALUES IN SEK AND MILLIONS
-- ============================================================================
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
        wip_balance,
        git_balance * price AS git_value_sek,
        wip_balance * price AS wip_value_sek,
        (git_balance * price) + (wip_balance * price) AS total_capital_sek,
        (git_balance * price) / 1000000.0 AS git_value_m_sek,
        (wip_balance * price) / 1000000.0 AS wip_value_m_sek,
        ((git_balance * price) + (wip_balance * price)) / 1000000.0 AS total_capital_m_sek
    FROM events_with_balances
),

-- ============================================================================
-- STEP 9: CREATE DAILY TIMELINE
-- ============================================================================

calendar AS (
    SELECT DATEADD(day, SEQ4(), CURRENT_DATE()) AS calendar_date
    FROM TABLE(GENERATOR(ROWCOUNT => 450))
    WHERE DATEADD(day, SEQ4(), CURRENT_DATE()) <= DATEADD(week, 60, CURRENT_DATE())
),

-- Get all unique parts
parts AS (
    SELECT DISTINCT site, part_number FROM final_events
),

-- Cartesian product: every part × every date
part_dates AS (
    SELECT
        p.site,
        p.part_number,
        c.calendar_date
    FROM parts p
    CROSS JOIN calendar c
),
-- For each (part, date), find the LAST event that occurred on or before that date
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
                     -- Match the order from events_with_balances
                     CASE fe.event_type
                         WHEN 'Starting Balance' THEN 1
                         WHEN 'Arrival' THEN 2
                         WHEN 'Pickup' THEN 3
                         WHEN 'Consumption' THEN 4
                     END DESC  -- DESC to pick highest number first
        ) AS rn
    FROM part_dates pd
    LEFT JOIN final_events fe
        ON fe.site = pd.site
        AND fe.part_number = pd.part_number
        AND fe.event_date <= pd.calendar_date
),

-- Keep only the most recent event per (part, date)
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

-- Daily timeline with part-level detail
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
        SUM(pd.part_amount) / NULLIF(COUNT(DISTINCT pd.production_day),0) AS avg_daily_usage
    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_DEMAND_AS_MANUFACTURED.PART_DEMAND_AS_MANUFACTURED_CURRENT_DATE pd
    WHERE pd.PRODUCTION_DAY >= DATEADD(day, -30, CURRENT_DATE())
    GROUP BY pd.site, pd.part_number
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
    pi.MFG_SUPPLIER_CODE,

    -- Safety Stock
    pi.safety_stock_nr_of_parts AS safety_stock_nr_of_parts,
    pi.SAFETY_STOCK_LEAD_TIME AS safety_stock_lead_time,

    -- Deviation vs. Safety Stock
    dt.wip_balance - pi.safety_stock_nr_of_parts AS wip_minus_ss,

    -- Days until stockout
    CASE
    WHEN dt.wip_balance <= 0 THEN 0
    WHEN du.avg_daily_usage > 0
        THEN dt.wip_balance / du.avg_daily_usage
    ELSE NULL
END AS days_until_stockout,

    -- Capital at risk calculation
    CASE
        WHEN dt.wip_balance < pi.safety_stock_nr_of_parts
        THEN (pi.safety_stock_nr_of_parts - dt.wip_balance) * dt.price
        ELSE 0
    END AS capital_at_risk_sek,

    
    -- Status flag
    CASE 
        WHEN dt.wip_balance < pi.safety_stock_nr_of_parts THEN 'Below SS'
        WHEN dt.wip_balance = pi.safety_stock_nr_of_parts THEN 'At SS'
        ELSE 'Above SS'
    END AS ss_deviation_flag
    

    
    
FROM daily_timeline dt
LEFT JOIN MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED pi
    ON dt.part_number = pi.PART_NUMBER
    AND dt.site = pi.SITE
LEFT JOIN daily_usage du
    ON dt.part_number = du.part_number
    AND dt.site = du.site
    
WHERE dt.site = 'VCT' 
ORDER BY  dt.total_capital_m_sek DESC,
    dt.part_number;

        ";

        // Execute query using SnowflakeService
        var dataTable = await _snowflakeService.QueryAsync(sql);
        
        // Convert DataTable to List<ForecastItem>
        var items = new List<ForecastItem>();
        foreach (DataRow row in dataTable.Rows)
        {
            items.Add(new ForecastItem
            {
                Site = row["SITE"]?.ToString(),
                Part_Number = row["PART_NUMBER"]?.ToString(),
                Date = row["DATE"] != DBNull.Value ? Convert.ToDateTime(row["DATE"]) : DateTime.MinValue,
                Git_Balance = row["GIT_BALANCE"] != DBNull.Value ? Convert.ToDecimal(row["GIT_BALANCE"]) : 0,
                Wip_Balance = row["WIP_BALANCE"] != DBNull.Value ? Convert.ToDecimal(row["WIP_BALANCE"]) : 0,
                Price = row["PRICE"] != DBNull.Value ? Convert.ToDecimal(row["PRICE"]) : 0,
                Git_Value_Sek = row["GIT_VALUE_SEK"] != DBNull.Value ? Convert.ToDecimal(row["GIT_VALUE_SEK"]) : 0,
                Wip_Value_Sek = row["WIP_VALUE_SEK"] != DBNull.Value ? Convert.ToDecimal(row["WIP_VALUE_SEK"]) : 0,
                Git_Value_M_Sek = row["GIT_VALUE_M_SEK"] != DBNull.Value ? Convert.ToDecimal(row["GIT_VALUE_M_SEK"]) : 0,
                Wip_Value_M_Sek = row["WIP_VALUE_M_SEK"] != DBNull.Value ? Convert.ToDecimal(row["WIP_VALUE_M_SEK"]) : 0,
                Total_Capital_M_Sek = row["TOTAL_CAPITAL_M_SEK"] != DBNull.Value ? Convert.ToDecimal(row["TOTAL_CAPITAL_M_SEK"]) : 0,
                Mfg_Supplier_Code = row["MFG_SUPPLIER_CODE"]?.ToString(),
                Safety_Stock_Nr_Of_Parts = row["SAFETY_STOCK_NR_OF_PARTS"] != DBNull.Value ? Convert.ToDecimal(row["SAFETY_STOCK_NR_OF_PARTS"]) : null,
                Safety_Stock_Lead_Time = row["SAFETY_STOCK_LEAD_TIME"] != DBNull.Value ? Convert.ToDecimal(row["SAFETY_STOCK_LEAD_TIME"]) : null,
                Wip_Minus_Ss = row["WIP_MINUS_SS"] != DBNull.Value ? Convert.ToDecimal(row["WIP_MINUS_SS"]) : null,
                Days_Until_Stockout = row["DAYS_UNTIL_STOCKOUT"] != DBNull.Value ? Convert.ToDecimal(row["DAYS_UNTIL_STOCKOUT"]) : null,
                Capital_At_Risk_Sek = row["CAPITAL_AT_RISK_SEK"] != DBNull.Value ? Convert.ToDecimal(row["CAPITAL_AT_RISK_SEK"]) : null,
                Ss_Deviation_Flag = row["SS_DEVIATION_FLAG"]?.ToString()
            });
        }

        // Post-process: Update prices from MySQL automation database
        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.Part_Number))
            {
                if (priceMap.TryGetValue(item.Part_Number, out var mysqlPrice) && mysqlPrice > 0)
                {
                    item.Price = mysqlPrice;
                    
                    // Recalculate all price-dependent values
                    item.Git_Value_Sek = item.Git_Balance * item.Price;
                    item.Wip_Value_Sek = item.Wip_Balance * item.Price;
                    item.Git_Value_M_Sek = item.Git_Value_Sek / 1000000.0m;
                    item.Wip_Value_M_Sek = item.Wip_Value_Sek / 1000000.0m;
                    item.Total_Capital_M_Sek = item.Git_Value_M_Sek + item.Wip_Value_M_Sek;
                    
                    // Recalculate capital at risk
                    if (item.Wip_Balance < (item.Safety_Stock_Nr_Of_Parts ?? 0))
                    {
                        item.Capital_At_Risk_Sek = ((item.Safety_Stock_Nr_Of_Parts ?? 0) - item.Wip_Balance) * item.Price;
                    }
                    else
                    {
                        item.Capital_At_Risk_Sek = 0;
                    }
                }
            }
        }

        return items;
    }
}