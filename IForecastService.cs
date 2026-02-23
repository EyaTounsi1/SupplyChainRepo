using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartTracker;

public interface IForecastService
{
    Task<List<ForecastItem>> GetForecastDataAsync();
    Task<List<ForecastItem>> GetForecastDataAsync(string? siteFilter, string? partNumberFilter, int months = 3);
}