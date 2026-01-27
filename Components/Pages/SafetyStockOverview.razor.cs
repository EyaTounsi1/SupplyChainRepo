using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Snowflake.Data.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PartTracker.Components.Pages
{
    public class ChartSeries
    {
        public string Name { get; set; }
        public double[] Data { get; set; }
    }
    public partial class SafetyStockOverview : ComponentBase
    {
        [Inject] IConfiguration Configuration { get; set; }

        private List<string> PartNumbers = new();
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

        public List<ChartSeries> ChartDatasets => new List<ChartSeries>
        {
            new ChartSeries { Name = "Actual Demand", Data = ActualDemandSeries.ToArray() },
            new ChartSeries { Name = "Safety Stock", Data = SafetyStockSeries.ToArray() }
        };

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

// ...existing code...

