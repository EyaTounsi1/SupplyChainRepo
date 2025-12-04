using PartTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartTracker;

public interface ISafetyStockService
{
    Task<List<SafetyStockItem>> GetSafetyStockDataAsync();
    Task<bool> TestConnectionAsync();
}