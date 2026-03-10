using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartTracker;

public interface IForecastService
{
    Task<List<ForecastItem>> GetForecastDataAsync();
    Task<List<ForecastItem>> GetForecastDataAsync(string? siteFilter, string? partNumberFilter, string? mfgCodeFilter, int months = 3, double demandMultiplier = 1.0, int leadTimeShiftDays = 0, double? ssOverrideParts = null, double? ssOverrideLeadTime = null);
}