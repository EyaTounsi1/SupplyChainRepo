using System.Threading;
using System.Threading.Tasks;

namespace PartTracker;

public interface IExcelImportService
{
    Task ImportChangeLogAsync(CancellationToken cancellationToken = default);
}