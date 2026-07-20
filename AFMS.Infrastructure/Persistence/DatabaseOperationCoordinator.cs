using AFMS.Application.Abstractions;

namespace AFMS.Infrastructure.Persistence;

public sealed class DatabaseOperationCoordinator : IDatabaseOperationCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task<IAsyncDisposable> EnterWriteAsync(CancellationToken cancellationToken = default) => EnterAsync(cancellationToken);
    public Task<IAsyncDisposable> EnterMaintenanceAsync(CancellationToken cancellationToken = default) => EnterAsync(cancellationToken);

    private async Task<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Releaser(_gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IAsyncDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
