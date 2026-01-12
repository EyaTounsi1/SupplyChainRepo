using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using PartTracker;
using PartTracker.Models;

namespace PartTracker.Components.Pages
{
    public partial class PremiumBookings : ComponentBase
    {
        [Inject] private ExcelImportService ExcelImportService { get; set; } = default!;
        [Inject] private AppDbContext DbContext { get; set; } = default!;
        [Inject] private AutomationDbContext AutomationDbContext { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        
        private int yesterdaybooking;
        private List<PremiumCsvRow> missingFormBookings = new();
        private List<PersonBookingCount> bookingsPerPerson = new();
        private int maxBookings = 1;
        private List<PersonCostCount> costPerPerson = new();
        private decimal maxCost = 1;
        private string selectedTimeFrame = "all";
        private string selectedCostTimeFrame = "all";
        private List<ReasonCodeCount> reasonCodesPerSCC = new();
        private int maxReasonCodes = 1;
        private List<string> trendLabels = new();
        private List<decimal> trendCostData = new();
        private List<int> trendCountData = new();
        private bool chartDrawn = false;
        private bool isLoading = true;
        private Dictionary<string, List<int>> reasonCodeTrendsData = new();
        private List<string> reasonCodeTrendLabels = new();
        private bool reasonCodeChartDrawn = false;
        private List<string> reasonCodeLabels = new();
        private List<int> reasonCodeCounts = new();
        private bool reasonCodePieChartDrawn = false;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                yesterdaybooking = await AutomationDbContext.Splunk
                    .CountAsync(e => e.CreatedAt.HasValue 
                                && e.CreatedAt.Value.Date == today
                                && !(e.DeliveryContactPerson ?? "").Contains("Emmy Wessberg"));

                // Get bookings per person from all splunk data
                await LoadBookingsPerPerson();
                await LoadCostPerPerson();
                await LoadReasonCodesPerSCC();
                await LoadTrendsData();
                await LoadReasonCodeTrends();
                await LoadReasonCodeCounts();

                // Find Splunk entries from today that are not in the Activity table
                // and have a Helper entry with 004TMOR Reason Code starting with VCC
                var todaySplunkEntries = await AutomationDbContext.Splunk
                    .Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value.Date == today)
                    .Where(e => !string.IsNullOrWhiteSpace(e.TransportOrder))
                    .Select(e => new { e.TransportOrder, e.DeliveryContactPerson })
                    .Distinct()
                    .ToListAsync();

                var activityToNumbers = await AutomationDbContext.Activity
                    .Where(a => !string.IsNullOrWhiteSpace(a.TONumber))
                    .Select(a => a.TONumber!.Trim())
                    .Distinct()
                    .ToListAsync();

                var helperEntries = await AutomationDbContext.Helper
                    .Where(h => !string.IsNullOrWhiteSpace(h.OrderNumber))
                    .Where(h => (h.ReasonCode ?? "").StartsWith("VCC"))
                    .Select(h => h.OrderNumber!.Trim())
                    .Distinct()
                    .ToListAsync();

                var activityToSet = new HashSet<string>(activityToNumbers, StringComparer.OrdinalIgnoreCase);
                var helperToSet = new HashSet<string>(helperEntries, StringComparer.OrdinalIgnoreCase);
                
                missingFormBookings = todaySplunkEntries
                    .Where(e => !activityToSet.Contains(e.TransportOrder!.Trim()))
                    .Where(e => helperToSet.Contains(e.TransportOrder!.Trim()))
                    .Where(e => !(e.DeliveryContactPerson ?? "").StartsWith("Emmy"))
                    .Where(e => !(e.DeliveryContactPerson ?? "").StartsWith("Wessberg"))
                    .Select(e => new PremiumCsvRow
                    {
                        TransportOrder = e.TransportOrder ?? string.Empty,
                        ContactPerson = e.DeliveryContactPerson ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading data: {ex.Message}");
            }
            finally
            {
                isLoading = false;
            }
        }

        private async Task LoadBookingsPerPerson()
        {
            var query = AutomationDbContext.Splunk
                .Where(e => !string.IsNullOrWhiteSpace(e.DeliveryContactPerson))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Emmy"))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Scc"))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Victor IBL"))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Abegail Antwi"));

            // Apply time frame filter
            var today = DateTime.Today;
            query = selectedTimeFrame switch
            {
                "week" => query.Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value >= today.AddDays(-7)),
                "month" => query.Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value >= today.AddMonths(-1)),
                "3months" => query.Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value >= today.AddMonths(-3)),
                _ => query // "all" - no filter
            };

            var rawData = await query.ToListAsync();
            
            bookingsPerPerson = rawData
                .GroupBy(e => NormalizePersonName(e.DeliveryContactPerson ?? ""))
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => new PersonBookingCount
                {
                    PersonName = g.First().DeliveryContactPerson!, // Use first occurrence's original name
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();
            
            maxBookings = bookingsPerPerson.Any() ? bookingsPerPerson.Max(x => x.Count) : 1;
        }

        private async Task LoadCostPerPerson()
        {
            var query = AutomationDbContext.Splunk
                .Where(e => !string.IsNullOrWhiteSpace(e.DeliveryContactPerson))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Emmy"))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Scc"))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Victor IBL"))
                .Where(e => !(e.DeliveryContactPerson ?? "").Contains("Abegail Antwi"))
                .Where(e => e.TotalCostEUR.HasValue && e.TotalCostEUR.Value > 0);

            // Apply time frame filter
            var today = DateTime.Today;
            query = selectedCostTimeFrame switch
            {
                "week" => query.Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value >= today.AddDays(-7)),
                "month" => query.Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value >= today.AddMonths(-1)),
                "3months" => query.Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value >= today.AddMonths(-3)),
                _ => query // "all" - no filter
            };

            var rawData = await query.ToListAsync();
            
            costPerPerson = rawData
                .GroupBy(e => NormalizePersonName(e.DeliveryContactPerson ?? ""))
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => new PersonCostCount
                {
                    PersonName = g.First().DeliveryContactPerson!, // Use first occurrence's original name
                    TotalCost = g.Sum(x => x.TotalCostEUR ?? 0)
                })
                .OrderByDescending(x => x.TotalCost)
                .ToList();
            
            maxCost = costPerPerson.Any() ? costPerPerson.Max(x => x.TotalCost) : 1;
        }

        private async Task LoadReasonCodesPerSCC()
        {
            try
            {
                reasonCodesPerSCC = await (from h in AutomationDbContext.Helper
                                          join s in AutomationDbContext.Splunk on h.OrderNumber equals s.TransportOrder
                                          where !string.IsNullOrWhiteSpace(h.ReasonCode)
                                          where !string.IsNullOrWhiteSpace(s.DeliveryContactPerson)
                                          where !s.DeliveryContactPerson.StartsWith("Emmy")
                                          where !s.DeliveryContactPerson.StartsWith("Wessberg")
                                          group h by new { s.DeliveryContactPerson, h.ReasonCode } into g
                                          select new ReasonCodeCount
                                          {
                                              SCC = g.Key.DeliveryContactPerson!,
                                              ReasonCode = g.Key.ReasonCode!,
                                              Count = g.Count()
                                          })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();
                
                maxReasonCodes = reasonCodesPerSCC.Any() ? reasonCodesPerSCC.Max(x => x.Count) : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading reason codes: {ex.Message}");
            }
        }

        private async Task OnTimeFrameChanged(string newTimeFrame)
        {
            selectedTimeFrame = newTimeFrame;
            await LoadBookingsPerPerson();
        }

        private async Task OnCostTimeFrameChanged(string newTimeFrame)
        {
            selectedCostTimeFrame = newTimeFrame;
            await LoadCostPerPerson();
        }

        private async Task LoadReasonCodeTrends()
        {
            try
            {
                // Join helper with splunk to get dates and reason codes
                var data = await (from h in AutomationDbContext.Helper
                                 join s in AutomationDbContext.Splunk on h.OrderNumber equals s.TransportOrder
                                 where !string.IsNullOrWhiteSpace(h.ReasonCode)
                                 where s.CreatedAt.HasValue
                                 select new
                                 {
                                     Date = s.CreatedAt!.Value.Date,
                                     ReasonCode = h.ReasonCode
                                 })
                    .ToListAsync();

                if (!data.Any())
                {
                    Console.WriteLine("No reason code trend data found");
                    return;
                }

                // Group by date and reason code
                var grouped = data
                    .GroupBy(x => new { x.Date, x.ReasonCode })
                    .Select(g => new
                    {
                        g.Key.Date,
                        g.Key.ReasonCode,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                // Get all unique dates and reason codes
                var dates = grouped.Select(x => x.Date).Distinct().OrderBy(d => d).ToList();
                var reasonCodes = grouped.Select(x => x.ReasonCode).Distinct().OrderBy(r => r).ToList();

                reasonCodeTrendLabels = dates.Select(d => d.ToString("MMM dd")).ToList();

                // Create datasets for each reason code
                reasonCodeTrendsData = new Dictionary<string, List<int>>();
                foreach (var reasonCode in reasonCodes)
                {
                    var counts = new List<int>();
                    foreach (var date in dates)
                    {
                        var count = grouped
                            .Where(x => x.Date == date && x.ReasonCode == reasonCode)
                            .Select(x => x.Count)
                            .FirstOrDefault();
                        counts.Add(count);
                    }
                    reasonCodeTrendsData[reasonCode!] = counts;
                }

                Console.WriteLine($"Loaded reason code trends: {reasonCodes.Count} codes across {dates.Count} dates");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading reason code trends: {ex.Message}");
            }
        }

        private async Task LoadReasonCodeCounts()
        {
            try
            {
                var data = await AutomationDbContext.Helper
                    .Where(h => !string.IsNullOrWhiteSpace(h.ReasonCode))
                    .GroupBy(h => h.ReasonCode)
                    .Select(g => new
                    {
                        ReasonCode = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                reasonCodeLabels = data.Select(x => x.ReasonCode!).ToList();
                reasonCodeCounts = data.Select(x => x.Count).ToList();

                Console.WriteLine($"Loaded {reasonCodeLabels.Count} reason codes for pie chart");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading reason code counts: {ex.Message}");
            }
        }

        private async Task LoadTrendsData()
        {
            try
            {
                Console.WriteLine("Starting LoadTrendsData...");
                
                var totalCount = await AutomationDbContext.Splunk.CountAsync();
                Console.WriteLine($"Total Splunk records: {totalCount}");
                
                var withDates = await AutomationDbContext.Splunk.CountAsync(e => e.CreatedAt.HasValue);
                Console.WriteLine($"Records with CreatedAt: {withDates}");
                
                var data = await AutomationDbContext.Splunk
                    .Where(e => e.CreatedAt.HasValue)
                    .GroupBy(e => e.CreatedAt!.Value.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        TotalCost = g.Sum(x => x.TotalCostEUR ?? 0),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                Console.WriteLine($"Grouped data points: {data.Count}");
                
                trendLabels = data.Select(x => x.Date.ToString("MMM dd")).ToList();
                trendCostData = data.Select(x => x.TotalCost).ToList();
                trendCountData = data.Select(x => x.Count).ToList();
                
                Console.WriteLine($"Loaded {trendLabels.Count} trend data points");
                if (trendLabels.Any())
                {
                    Console.WriteLine($"First date: {trendLabels.First()}, Last date: {trendLabels.Last()}");
                    Console.WriteLine($"First cost: {trendCostData.First()}, First count: {trendCountData.First()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadTrendsData: {ex.Message}");
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!isLoading && !chartDrawn && trendLabels.Any())
            {
                Console.WriteLine($"Drawing chart with {trendLabels.Count} labels");
                try
                {
                    await JSRuntime.InvokeVoidAsync("drawPremiumTrendsChart", "premiumTrendsChart", trendLabels, trendCostData, trendCountData);
                    chartDrawn = true;
                    Console.WriteLine("Chart drawn successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error drawing chart: {ex.Message}");
                }
            }

            if (!isLoading && !reasonCodeChartDrawn && reasonCodeTrendLabels.Any())
            {
                Console.WriteLine($"Drawing reason code chart with {reasonCodeTrendLabels.Count} labels");
                try
                {
                    await JSRuntime.InvokeVoidAsync("drawReasonCodeTrendsChart", "reasonCodeTrendsChart", reasonCodeTrendLabels, reasonCodeTrendsData);
                    reasonCodeChartDrawn = true;
                    Console.WriteLine("Reason code chart drawn successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error drawing reason code chart: {ex.Message}");
                }
            }

            if (!isLoading && !reasonCodePieChartDrawn && reasonCodeLabels.Any())
            {
                Console.WriteLine($"Drawing reason code pie chart with {reasonCodeLabels.Count} labels");
                try
                {
                    await JSRuntime.InvokeVoidAsync("drawReasonCodePieChart", "reasonCodePieChart", reasonCodeLabels, reasonCodeCounts);
                    reasonCodePieChartDrawn = true;
                    Console.WriteLine("Reason code pie chart drawn successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error drawing reason code pie chart: {ex.Message}");
                }
            }
        }

        private async Task<List<PremiumCsvRow>> ReadPremiumCsvAsync(string path)
        {
            var rows = new List<PremiumCsvRow>();
            if (!File.Exists(path)) return rows;

            // Copy to temp to avoid lock issues
            string tempPath = Path.Combine(Path.GetTempPath(), $"premium_csv_{Guid.NewGuid()}.csv");
            try
            {
                File.Copy(path, tempPath, overwrite: true);
            }
            catch
            {
                // If copy fails, try reading original
                tempPath = path;
            }

            var lines = await File.ReadAllLinesAsync(tempPath);
            if (lines.Length == 0) return rows;

            var header = SplitCsvLine(lines[0]);
            int toIdx = IndexOf(header, "Transport Order");
            if (toIdx < 0) toIdx = IndexOf(header, "Transport Orders");
            int contactIdx = IndexOf(header, "Contact Person");
            if (contactIdx < 0) contactIdx = IndexOf(header, "Contactperson");

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = SplitCsvLine(lines[i]);
                string transportOrder = (toIdx >= 0 && toIdx < cols.Count) ? cols[toIdx].Trim() : string.Empty;
                string contactPerson = (contactIdx >= 0 && contactIdx < cols.Count) ? cols[contactIdx].Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(transportOrder))
                {
                    rows.Add(new PremiumCsvRow { TransportOrder = transportOrder, ContactPerson = contactPerson });
                }
            }

            // Clean up temp file
            if (tempPath != path)
            {
                try { File.Delete(tempPath); } catch { }
            }

            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var list = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (c == ',' && !inQuotes)
                {
                    list.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            list.Add(current.ToString());
            return list;
        }

        private static int IndexOf(IList<string> cols, string name)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                if (string.Equals(cols[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string NormalizePersonName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            
            // Remove accents and normalize encoding issues (UTF-8 misinterpreted as Latin-1)
            // Multi-character replacements must come first
            var normalized = name
                .Replace("Ã¼", "ü")     // ü
                .Replace("Ã¶", "ö")     // ö
                .Replace("Ã¤", "a")     // ä
                .Replace("Ã¥", "å")     // å
                .Replace("Ã…", "Å")     // Å
                .Replace("Ã„", "Ä")     // Ä
                .Replace("Ã–", "Ö")     // Ö
                .Replace("Ã", "Ü")      // Ü
                .Trim();
            
            // Handle "LastName, FirstName" vs "FirstName LastName" variations
            // Convert all to "FirstName LastName" format
            if (normalized.Contains(","))
            {
                var parts = normalized.Split(',', 2);
                if (parts.Length == 2)
                {
                    normalized = $"{parts[1].Trim()} {parts[0].Trim()}";
                }
            }
            
            // Convert to lowercase for case-insensitive comparison
            var lowerName = normalized.ToLowerInvariant();
            
            // Handle known nickname/alias mappings
            if (lowerName.Contains("janne") && !lowerName.Contains("johannesson"))
            {
                return "jan-eric johannesson";
            }
            if (lowerName.Contains("jan-eric johannesson") || lowerName.Contains("johannesson"))
            {
                return "jan-eric johannesson";
            }
            
            // Ann-Sofie variations
            if (lowerName.Contains("ann-sofie"))
            {
                return "ann-sofie thorsson";
            }
            
            // Therese Färdigh variations
            if (lowerName.Contains("therese") && (lowerName.Contains("färdigh") || lowerName.Contains("fardigh")))
            {
                return "therese färdigh";
            }
            
            return lowerName;
        }

        private class PremiumCsvRow
        {
            public string TransportOrder { get; set; } = string.Empty;
            public string ContactPerson { get; set; } = string.Empty;
        }

        private class PersonBookingCount
        {
            public string PersonName { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private class PersonCostCount
        {
            public string PersonName { get; set; } = string.Empty;
            public decimal TotalCost { get; set; }
        }

        private class ReasonCodeCount
        {
            public string SCC { get; set; } = string.Empty; // Now represents Contact Person
            public string ReasonCode { get; set; } = string.Empty;
            public int Count { get; set; }
        }

    }
}