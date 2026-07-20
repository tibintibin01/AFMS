using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.Infrastructure.Persistence;

internal sealed class EmptyInitialDataProvider : IInitialDataProvider
{
    public Task<InitialDataSet?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<InitialDataSet?>(null);
}
