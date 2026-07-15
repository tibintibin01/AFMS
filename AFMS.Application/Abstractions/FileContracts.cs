namespace AFMS.Application.Abstractions;

public interface IApplicationPathService
{
    string DataDirectory { get; }
    string DatabasePath { get; }
    string LogDirectory { get; }
    void EnsureDirectoriesExist();
}
