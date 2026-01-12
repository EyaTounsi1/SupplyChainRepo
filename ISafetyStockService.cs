using PartTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartTracker;

public interface ISafetyStockService
{
    Task<List<SafetyStockItem>> GetSafetyStockDataAsync();
    Task<List<SafetyStockChange>> GetSafetyStockChangesAsync();
    Task<List<SafetyStockChange>> GetSafetyStockChangesAsync(string site, string planningPoint);
    Task<bool> TestConnectionAsync();
}