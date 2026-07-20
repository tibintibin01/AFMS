using AFMS.Application.Models;

namespace AFMS.Application.Abstractions;

public interface IInitialDataProvider
{
    Task<InitialDataSet?> GetAsync(CancellationToken cancellationToken = default);
}
