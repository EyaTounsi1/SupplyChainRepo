// SafetyStockOverview.razor.cs
// NOTE: This is the code-behind for your Blazor component.
// It loads:
// 1) Demand vs Safety Stock (forward-filled) for a selected part number
// 2) Aggregate Lead Time trend
// 3) Aggregate Quantity trend

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using PartTracker.Shared.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PartTracker.Components.Pages
{
    public partial class SafetyStockOverview : ComponentBase
    {
        [Inject] public SnowflakeService Snowflake { get; set; } = default!;

        // ---------- UI State ----------
        private bool isLoading = true;
        private bool isLoadingLeadTime = false;
        private bool isLoadingQuantity = false;

        public string? ErrorMessage { get; set; }

        // ---------- Part picker ----------
        public List<string> PartNumbers { get; set; } = new();
        public string SelectedPartNumber { get; set; } = string.Empty;

        // ---------- Demand vs Safety Stock chart ----------
        public List<string> DateLabels { get; set; } = new();
        public List<double> ActualDemandSeries { get; set; } = new();
        public List<double> SafetyStockSeries { get; set; } = new();

        public string[] ChartLabelsArray => DateLabels.ToArray();

        public List<MudBlazor.ChartSeries> ChartDatasets => new()
        {
            new MudBlazor.ChartSeries { Name = "Actual Demand", Data = ActualDemandSeries.ToArray() },
            new MudBlazor.ChartSeries { Name = "Safety Stock (Parts)", Data = SafetyStockSeries.ToArray() }
        };

        // ---------- Lead Time trend chart ----------
        public List<string> LeadTimeMonthLabels { get; set; } = new();
        public List<double> AvgLeadTimeData { get; set; } = new();
        public List<double> MedianLeadTimeData { get; set; } = new();

        public List<MudBlazor.ChartSeries> LeadTimeChartSeries => new()
        {
            new MudBlazor.ChartSeries { Name = "Average Lead Time (Shifts)", Data = AvgLeadTimeData.ToArray() },
            new MudBlazor.ChartSeries { Name = "Median Lead Time (Shifts)", Data = MedianLeadTimeData.ToArray() }
        };

        // ---------- Quantity trend chart ----------
        public List<string> QuantityMonthLabels { get; set; } = new();
        public List<double> TotalQuantityData { get; set; } = new();
        public List<double> AvgQuantityData { get; set; } = new();
        public List<double> MedianQuantityData { get; set; } = new();

        public List<MudBlazor.ChartSeries> QuantityChartSeries => new()
        {
            new MudBlazor.ChartSeries { Name = "Total SS Parts", Data = TotalQuantityData.ToArray() },
            new MudBlazor.ChartSeries { Name = "Average SS Parts", Data = AvgQuantityData.ToArray() },
            new MudBlazor.ChartSeries { Name = "Median SS Parts", Data = MedianQuantityData.ToArray() }
        };

        // ---------- Lifecycle ----------
        protected override async Task OnInitializedAsync()
        {
            isLoading = true;
            ErrorMessage = null;

            await LoadPartNumbersAsync();

            if (PartNumbers.Count > 0)
            {
                SelectedPartNumber = PartNumbers[0];
                await LoadDataForPartAsync(SelectedPartNumber);
            }

            isLoading = false;
            await InvokeAsync(StateHasChanged);

            // Load aggregate charts (fire-and-forget is OK here)
            _ = LoadLeadTimeTrendAsync();
            _ = LoadQuantityTrendAsync();
        }

        // Call this from your .razor on dropdown change (ValueChanged)
        public async Task OnPartNumberChanged(string? value)
        {
            var partNumber = value?.Trim() ?? string.Empty;
            SelectedPartNumber = partNumber;

            if (string.IsNullOrWhiteSpace(partNumber))
                return;

            isLoading = true;
            ErrorMessage = null;
            await InvokeAsync(StateHasChanged);

            await LoadDataForPartAsync(partNumber);

            isLoading = false;
            await InvokeAsync(StateHasChanged);
        }

        // ---------- Load Part Numbers ----------
        private async Task LoadPartNumbersAsync()
        {
            PartNumbers.Clear();
            ErrorMessage = null;

            try
            {
                var result = await Snowflake.QueryAsync(@"
                    SELECT DISTINCT PART_NUMBER
                    FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_CONSUMPTION_AS_MANUFACTURED.PART_CONSUMPTION_AS_MANUFACTURED
                    WHERE PART_NUMBER IS NOT NULL
                    ORDER BY PART_NUMBER
                    LIMIT 1000
                ");

                foreach (DataRow row in result.Rows)
                {
                    var part = row[0]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(part))
                        PartNumbers.Add(part.Trim());
                }
            }
            catch (Exception)
            {
                ErrorMessage = "Failed to load part numbers.";
                // throw; // uncomment while debugging if you want hard failures
            }
        }

        // ---------- Demand vs Safety Stock (forward-filled) ----------
        // Uses ALL THREE tables:
        // - part_scope from PART_SUPPLIER_INFORMATION_AS_MANUFACTURED
        // - demand from PART_CONSUMPTION_AS_MANUFACTURED
        // - safety stock history from SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL
        private async Task LoadDataForPartAsync(string partNumber)
        {
            DateLabels.Clear();
            ActualDemandSeries.Clear();
            SafetyStockSeries.Clear();
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(partNumber))
                return;

            try
            {
                var result = await Snowflake.QueryAsync($@"
WITH part_scope AS (
  SELECT SITE, PLANNING_POINT, PART_NUMBER, MFG_SUPPLIER_CODE
  FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_SUPPLIER_INFORMATION_AS_MANUFACTURED.PART_SUPPLIER_INFORMATION_AS_MANUFACTURED
  WHERE PART_NUMBER = '{partNumber.Trim()}'
),
demand AS (
  SELECT
    c.PART_CONSUMED_DATE AS DT,
    SUM(c.PART_AMOUNT)::FLOAT AS DEMAND
  FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_CONSUMPTION_AS_MANUFACTURED.PART_CONSUMPTION_AS_MANUFACTURED c
  JOIN part_scope p
    ON c.SITE = p.SITE
   AND c.PLANNING_POINT = p.PLANNING_POINT
   AND c.PART_NUMBER = p.PART_NUMBER
  GROUP BY 1
),
ss_daily AS (
  SELECT
    h.UPLOADED_FROM_SOURCE AS DT,
    h.SAFETY_STOCK_NR_OF_PARTS::FLOAT AS SS_PARTS,
    ROW_NUMBER() OVER (
      PARTITION BY h.UPLOADED_FROM_SOURCE, h.SITE, h.PLANNING_POINT, h.PART_NUMBER, h.MFG_SUPPLIER_CODE
      ORDER BY h.UPLOADED_FROM_SOURCE DESC
    ) AS RN
  FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL h
  JOIN part_scope p
    ON h.SITE = p.SITE
   AND h.PLANNING_POINT = p.PLANNING_POINT
   AND h.PART_NUMBER = p.PART_NUMBER
   AND h.MFG_SUPPLIER_CODE = p.MFG_SUPPLIER_CODE
),
ss AS (
  SELECT DT, SS_PARTS
  FROM ss_daily
  WHERE RN = 1
),
bounds AS (
  SELECT
    LEAST(
      COALESCE((SELECT MIN(DT) FROM demand), '2999-12-31'::DATE),
      COALESCE((SELECT MIN(DT) FROM ss),     '2999-12-31'::DATE)
    ) AS MIN_DT,
    GREATEST(
      COALESCE((SELECT MAX(DT) FROM demand), '1900-01-01'::DATE),
      COALESCE((SELECT MAX(DT) FROM ss),     '1900-01-01'::DATE)
    ) AS MAX_DT
),
spine AS (
  SELECT DATEADD(day, seq4(), b.MIN_DT) AS DT, b.MIN_DT, b.MAX_DT
  FROM bounds b,
       TABLE(GENERATOR(ROWCOUNT => 10000))
  WHERE DATEADD(day, seq4(), b.MIN_DT) <= b.MAX_DT
)
SELECT
  s.DT,
  COALESCE(d.DEMAND, 0) AS DEMAND,
  LAST_VALUE(ss.SS_PARTS IGNORE NULLS) OVER (ORDER BY s.DT) AS SAFETY_STOCK_PARTS
FROM spine s
LEFT JOIN demand d ON d.DT = s.DT
LEFT JOIN ss     ON ss.DT = s.DT
ORDER BY s.DT;
");

                foreach (DataRow row in result.Rows)
                {
                    var dt = Convert.ToDateTime(row[0]);
                    var demand = Convert.ToDouble(row[1]);
                    var ssParts = Convert.ToDouble(row[2]);

                    DateLabels.Add(dt.ToString("yyyy-MM-dd"));
                    ActualDemandSeries.Add(demand);
                    SafetyStockSeries.Add(ssParts);
                }

                Console.WriteLine($"Loaded {DateLabels.Count} rows for part {partNumber}");
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load demand vs safety stock data.";
                Console.WriteLine("LoadDataForPartAsync FAILED:");
                Console.WriteLine(ex.ToString());
                throw; // keep throwing while you debug
            }
            finally
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        // ---------- Aggregate: Lead Time Trend ----------
        private async Task LoadLeadTimeTrendAsync()
        {
            isLoadingLeadTime = true;
            LeadTimeMonthLabels.Clear();
            AvgLeadTimeData.Clear();
            MedianLeadTimeData.Clear();
            ErrorMessage = null;

            try
            {
                var result = await Snowflake.QueryAsync(@"
WITH filtered_parts AS (
  SELECT
      SITE,
      PLANNING_POINT,
      PART_NUMBER,
      MFG_SUPPLIER_CODE
  FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_SUPPLIER_INFORMATION_AS_MANUFACTURED.PART_SUPPLIER_INFORMATION_AS_MANUFACTURED
  WHERE SITE = 'VCT'
    AND MFG_SUPPLIER_CODE NOT ILIKE '%BSNRA%'
    AND COALESCE(SEQUENCE_PARTS_FLAG, '') <> 'Sequence Part'
),
monthly_snapshot AS (
  SELECT
      h.SITE,
      h.PLANNING_POINT,
      h.PART_NUMBER,
      h.MFG_SUPPLIER_CODE,
      DATE_TRUNC('month', h.UPLOADED_FROM_SOURCE) AS MONTH,
      COALESCE(NULLIF(h.FLS_YARD_LEADTIME_SHIFTS_CALC, 0), h.SAFETY_STOCK_LEAD_TIME) AS EFFECTIVE_SS_SHIFTS,
      h.SAFETY_STOCK_NR_OF_PARTS AS SS_PARTS,
      h.UPLOADED_FROM_SOURCE
  FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL h
  INNER JOIN filtered_parts p
    ON  h.SITE = p.SITE
    AND h.PLANNING_POINT = p.PLANNING_POINT
    AND h.PART_NUMBER = p.PART_NUMBER
    AND h.MFG_SUPPLIER_CODE = p.MFG_SUPPLIER_CODE
  QUALIFY ROW_NUMBER() OVER (
    PARTITION BY h.SITE, h.PLANNING_POINT, h.PART_NUMBER, h.MFG_SUPPLIER_CODE, DATE_TRUNC('month', h.UPLOADED_FROM_SOURCE)
    ORDER BY h.UPLOADED_FROM_SOURCE DESC
  ) = 1
)
SELECT
  MONTH,
  COUNT(*) AS PART_SUPPLIER_ROWS,
  AVG(EFFECTIVE_SS_SHIFTS) AS AVG_EFFECTIVE_SS_SHIFTS,
  MEDIAN(EFFECTIVE_SS_SHIFTS) AS MEDIAN_EFFECTIVE_SS_SHIFTS
FROM monthly_snapshot
GROUP BY 1
ORDER BY 1;
");

                foreach (DataRow row in result.Rows)
                {
                    var month = Convert.ToDateTime(row[0]);
                    var avg = Convert.ToDouble(row[2]);
                    var median = Convert.ToDouble(row[3]);

                    LeadTimeMonthLabels.Add(month.ToString("MMM yyyy"));
                    AvgLeadTimeData.Add(avg);
                    MedianLeadTimeData.Add(median);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadLeadTimeTrendAsync error:");
                Console.WriteLine(ex.ToString());
                ErrorMessage = $"Failed to load lead time trend: {ex.Message}";
            }
            finally
            {
                isLoadingLeadTime = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        // ---------- Aggregate: Quantity Trend ----------
        private async Task LoadQuantityTrendAsync()
        {
            isLoadingQuantity = true;
            QuantityMonthLabels.Clear();
            TotalQuantityData.Clear();
            AvgQuantityData.Clear();
            MedianQuantityData.Clear();
            ErrorMessage = null;

            try
            {
                var result = await Snowflake.QueryAsync(@"
WITH filtered_parts AS (
  SELECT
      SITE,
      PLANNING_POINT,
      PART_NUMBER,
      MFG_SUPPLIER_CODE
  FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_SUPPLIER_INFORMATION_AS_MANUFACTURED.PART_SUPPLIER_INFORMATION_AS_MANUFACTURED
  WHERE SITE = 'VCT'
    AND MFG_SUPPLIER_CODE NOT ILIKE '%BSNRA%'
    AND COALESCE(SEQUENCE_PARTS_FLAG, '') <> 'Sequence Part'
),
monthly_snapshot AS (
  SELECT
      h.SITE,
      h.PLANNING_POINT,
      h.PART_NUMBER,
      h.MFG_SUPPLIER_CODE,
      DATE_TRUNC('month', h.UPLOADED_FROM_SOURCE) AS MONTH,
      COALESCE(NULLIF(h.FLS_YARD_LEADTIME_SHIFTS_CALC, 0), h.SAFETY_STOCK_LEAD_TIME) AS EFFECTIVE_SS_SHIFTS,
      h.SAFETY_STOCK_NR_OF_PARTS AS SS_PARTS,
      h.UPLOADED_FROM_SOURCE
  FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL h
  INNER JOIN filtered_parts p
    ON  h.SITE = p.SITE
    AND h.PLANNING_POINT = p.PLANNING_POINT
    AND h.PART_NUMBER = p.PART_NUMBER
    AND h.MFG_SUPPLIER_CODE = p.MFG_SUPPLIER_CODE
  QUALIFY ROW_NUMBER() OVER (
    PARTITION BY h.SITE, h.PLANNING_POINT, h.PART_NUMBER, h.MFG_SUPPLIER_CODE, DATE_TRUNC('month', h.UPLOADED_FROM_SOURCE)
    ORDER BY h.UPLOADED_FROM_SOURCE DESC
  ) = 1
)
SELECT
  MONTH,
  COUNT(*) AS PART_SUPPLIER_ROWS,
  SUM(SS_PARTS) AS TOTAL_SS_PARTS,
  AVG(SS_PARTS) AS AVG_SS_PARTS,
  MEDIAN(SS_PARTS) AS MEDIAN_SS_PARTS
FROM monthly_snapshot
GROUP BY 1
ORDER BY 1;
");

                foreach (DataRow row in result.Rows)
                {
                    var month = Convert.ToDateTime(row[0]);
                    var total = Convert.ToDouble(row[2]);
                    var avg = Convert.ToDouble(row[3]);
                    var median = Convert.ToDouble(row[4]);

                    QuantityMonthLabels.Add(month.ToString("MMM yyyy"));
                    TotalQuantityData.Add(total);
                    AvgQuantityData.Add(avg);
                    MedianQuantityData.Add(median);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadQuantityTrendAsync error:");
                Console.WriteLine(ex.ToString());
                ErrorMessage = $"Failed to load quantity trend: {ex.Message}";
            }
            finally
            {
                isLoadingQuantity = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }
}
