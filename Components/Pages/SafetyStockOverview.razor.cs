    // ...usings...
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Snowflake.Data.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PartTracker.Components.Pages
{
    public partial class SafetyStockOverview : ComponentBase
    {
        public string[] ChartLabelsArray => DateLabels.ToArray();
        public List<MudBlazor.ChartSeries> ChartDatasets => new List<MudBlazor.ChartSeries>
        {
            new MudBlazor.ChartSeries { Name = "Actual Demand", Data = ActualDemandSeries.ToArray() },
            new MudBlazor.ChartSeries { Name = "Safety Stock", Data = SafetyStockSeries.ToArray() }
        };
        [Inject] IConfiguration Configuration { get; set; }

        public List<string> PartNumbers { get; set; } = new();
        // Use property with backing field in .razor
        // private string SelectedPartNumber; // REMOVED, use property only
        private bool isLoading = true;
        private List<DateTime> Dates = new();
        private List<int> ActualDemand = new();
        private List<double> SafetyStock = new();

        // For charting
        public List<string> DateLabels { get; set; } = new();
        public List<double> SafetyStockSeries { get; set; } = new();
        public List<double> ActualDemandSeries { get; set; } = new();

        // Lead Time Chart Data
        private bool isLoadingLeadTime = false;
        public List<string> LeadTimeMonthLabels { get; set; } = new();
        public List<double> AvgLeadTimeData { get; set; } = new();
        public List<double> MedianLeadTimeData { get; set; } = new();
        public List<MudBlazor.ChartSeries> LeadTimeChartSeries => new List<MudBlazor.ChartSeries>
        {
            new MudBlazor.ChartSeries { Name = "Average Lead Time (Shifts)", Data = AvgLeadTimeData.ToArray() },
            new MudBlazor.ChartSeries { Name = "Median Lead Time (Shifts)", Data = MedianLeadTimeData.ToArray() }
        };

        // Quantity Chart Data
        private bool isLoadingQuantity = false;
        public List<string> QuantityMonthLabels { get; set; } = new();
        public List<double> TotalQuantityData { get; set; } = new();
        public List<double> AvgQuantityData { get; set; } = new();
        public List<double> MedianQuantityData { get; set; } = new();
        public List<MudBlazor.ChartSeries> QuantityChartSeries => new List<MudBlazor.ChartSeries>
        {
            new MudBlazor.ChartSeries { Name = "Total SS Parts", Data = TotalQuantityData.ToArray() },
            new MudBlazor.ChartSeries { Name = "Average SS Parts", Data = AvgQuantityData.ToArray() },
            new MudBlazor.ChartSeries { Name = "Median SS Parts", Data = MedianQuantityData.ToArray() }
        };

        // Removed ChartDatasets, not needed for MudChart Data/XAxisLabels

        protected override async Task OnInitializedAsync()
        {
            isLoading = true;
            await LoadPartNumbersAsync();
            if (PartNumbers.Count > 0)
            {
                SelectedPartNumber = PartNumbers[0];
                await LoadDataForPartAsync(SelectedPartNumber);
            }

            isLoading = false;

            // Load aggregate charts
            _ = LoadLeadTimeTrendAsync();
            _ = LoadQuantityTrendAsync();
        }

        private async Task OnPartNumberChanged(string partNumber)
        {
            // SelectedPartNumber is set by the property setter
            isLoading = true;
            await LoadDataForPartAsync(partNumber);

            isLoading = false;
        }

        private string GetSnowflakeConnectionString()
        {
            // Try to get from ConnectionStrings or build from Snowflake section
            var connStr = Configuration.GetConnectionString("SnowflakeConnection2");
            if (!string.IsNullOrEmpty(connStr))
                return connStr;

            // Build from Snowflake section
            var account = Configuration["Snowflake:Account"];
            var user = Configuration["Snowflake:User"];
            var password = Configuration["Snowflake:Password"];
            var warehouse = Configuration["Snowflake:Warehouse"];
            var database = Configuration["Snowflake:Database"];
            var schema = Configuration["Snowflake:Schema"];
            var role = Configuration["Snowflake:Role"];
            return $"account={account};user={user};password={password};warehouse={warehouse};database={database};schema={schema};role={role}";
        }

        private async Task LoadPartNumbersAsync()
        {
            PartNumbers.Clear();
            try
            {
                using var conn = new SnowflakeDbConnection();
                conn.ConnectionString = GetSnowflakeConnectionString();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT DISTINCT PART_NUMBER FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_CONSUMPTION_AS_MANUFACTURED.PART_CONSUMPTION_AS_MANUFACTURED ORDER BY PART_NUMBER LIMIT 1000";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var part = reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(part))
                        PartNumbers.Add(part);
                }
            }
            catch (Exception ex)
            {
                // Handle error (log, show message, etc.)
            }
        }
        private async Task LoadDataForPartAsync(string partNumber)
        {
            Dates.Clear();
            ActualDemand.Clear();
            SafetyStock.Clear();
            DateLabels.Clear();
            SafetyStockSeries.Clear();
            ActualDemandSeries.Clear();
            try
            {
                using var conn = new SnowflakeDbConnection();
                conn.ConnectionString = GetSnowflakeConnectionString();
                await conn.OpenAsync();

                // Get demand (time series)
                var demandDict = new Dictionary<DateTime, int>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
                        SELECT PART_CONSUMED_DATE, SUM(PART_AMOUNT) AS TOTAL_CONSUMED
                        FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.PART_CONSUMPTION_AS_MANUFACTURED.PART_CONSUMPTION_AS_MANUFACTURED
                        WHERE PART_NUMBER = @partNumber
                        GROUP BY PART_CONSUMED_DATE
                        ORDER BY PART_CONSUMED_DATE
                    ";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@partNumber";
                    param.Value = partNumber;
                    cmd.Parameters.Add(param);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var date = reader.GetDateTime(0);
                        var demand = Convert.ToInt32(reader.GetValue(1));
                        demandDict[date] = demand;
                    }
                }

                // Get safety stock per day (historical)
                var safetyDict = new Dictionary<DateTime, double>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
                        SELECT UPLOADED_FROM_SOURCE, SAFETY_STOCK_NR_OF_PARTS
                        FROM MANUFACTURING_ENTERPRISE_DATA_PRODUCTS.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED.SAFETY_STOCK_SETTINGS_AS_MANUFACTURED_HISTORICAL
                        WHERE PART_NUMBER = @partNumber
                        ORDER BY UPLOADED_FROM_SOURCE
                    ";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@partNumber";
                    param.Value = partNumber;
                    cmd.Parameters.Add(param);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var date = reader.GetDateTime(0);
                        var stock = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        safetyDict[date] = stock;
                    }
                }

                // Merge dates from both series
                var allDates = new SortedSet<DateTime>(demandDict.Keys);
                foreach (var d in safetyDict.Keys) allDates.Add(d);
                if (allDates.Count == 0) return;

                // Forward-fill safety stock for each date
                double lastStock = 0;
                foreach (var date in allDates)
                {
                    int demand = demandDict.TryGetValue(date, out var d) ? d : 0;
                    double stock = lastStock;
                    if (safetyDict.TryGetValue(date, out var s))
                        stock = s;
                    lastStock = stock;

                    Dates.Add(date);
                    DateLabels.Add(date.ToString("yyyy-MM-dd"));
                    ActualDemand.Add(demand);
                    ActualDemandSeries.Add(demand);
                    SafetyStock.Add(stock);
                    SafetyStockSeries.Add(stock);
                }
            }
            catch (Exception ex)
            {
                // Handle error (log, show message, etc.)
            }
        }
        private async Task LoadLeadTimeTrendAsync()
        {
            isLoadingLeadTime = true;
            LeadTimeMonthLabels.Clear();
            AvgLeadTimeData.Clear();
            MedianLeadTimeData.Clear();

            try
            {
                using var conn = new SnowflakeDbConnection();
                conn.ConnectionString = GetSnowflakeConnectionString();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
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
ORDER BY 1";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var month = reader.GetDateTime(0);
                    var avg = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2));
                    var median = reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3));

                    LeadTimeMonthLabels.Add(month.ToString("MMM yyyy"));
                    AvgLeadTimeData.Add(avg);
                    MedianLeadTimeData.Add(median);
                }
            }
            catch (Exception ex)
            {
                // Handle error
            }
            finally
            {
                isLoadingLeadTime = false;
                StateHasChanged();
            }
        }

        private async Task LoadQuantityTrendAsync()
        {
            isLoadingQuantity = true;
            QuantityMonthLabels.Clear();
            TotalQuantityData.Clear();
            AvgQuantityData.Clear();
            MedianQuantityData.Clear();

            try
            {
                using var conn = new SnowflakeDbConnection();
                conn.ConnectionString = GetSnowflakeConnectionString();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
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
ORDER BY 1";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var month = reader.GetDateTime(0);
                    var total = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2));
                    var avg = reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3));
                    var median = reader.IsDBNull(4) ? 0 : Convert.ToDouble(reader.GetValue(4));

                    QuantityMonthLabels.Add(month.ToString("MMM yyyy"));
                    TotalQuantityData.Add(total);
                    AvgQuantityData.Add(avg);
                    MedianQuantityData.Add(median);
                }
            }
            catch (Exception ex)
            {
                // Handle error
            }
            finally
            {
                isLoadingQuantity = false;
                StateHasChanged();
            }
        }

        private string selectedPartNumber;
        public string SelectedPartNumber
        {
            get => selectedPartNumber;
            set
            {
                if (selectedPartNumber != value)
                {
                    selectedPartNumber = value;
                    _ = OnPartNumberChanged(value);
                }
            }
        }

    }
}


