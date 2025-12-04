using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartTracker;

public interface IForecastService
{
    Task<List<ForecastItem>> GetForecastDataAsync();
}