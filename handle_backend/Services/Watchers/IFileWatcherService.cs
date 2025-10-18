namespace handle_backend.Services
{
    public interface IFileWatcherService
    {
        Task StartAsync();
        Task StopAsync();
        int GetQueueCount();
        bool IsRunning { get; }
    }
}