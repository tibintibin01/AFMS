using AFMS.Application.Models;

namespace AFMS.Application.Abstractions;

public interface IDashboardQueryService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default);
}
