using handle_backend.Services.XML;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;

namespace handle_backend.Services
{
    public class FileWatcherService : IFileWatcherService, IDisposable
    {
        private readonly string _folderPath = @"D:\XML_Data\QuyetDinh_4750_2023_HSKCB"; //Thay đổi theo file cá nhân
        private readonly HandleXML _handleXML;
        private static readonly HashSet<string> _processedFiles = new();
        private BlockingCollection<string> _fileQueue = new();
        private CancellationTokenSource _cts = new();
        private FileSystemWatcher? _watcher;
        private bool _isRunning;
        private readonly ILogger<FileWatcherService> _logger;
        private readonly object _lock = new();

        public FileWatcherService(HandleXML handleXML, ILogger<FileWatcherService> logger)
        {
            _handleXML = handleXML;
            _logger = logger;
        }

        public bool IsRunning => _isRunning;

        public async Task StartAsync()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
            }

            // ✅ RESET nếu cần
            if (_cts.IsCancellationRequested) _cts = new();
            if (_fileQueue.IsAddingCompleted) _fileQueue = new();

            // 🚀 CRITICAL FIX: SETUP WATCHER TRƯỚC
            SetupWatcher();

            // 🔥 PROCESS 10K FILES CŨ - ĐÂY LÀ PHẦN QUAN TRỌNG!
            await ProcessExistingFilesAsync();

            // ✅ START QUEUE PROCESSING
            _ = Task.Run(() => ProcessQueue(_cts.Token));

            _logger.LogInformation("🩺 FileWatcher STARTED - Processed {Count} existing files",
                _processedFiles.Count);
        }

        // ✅ NEW: SETUP WATCHER
        private void SetupWatcher()
        {
            _watcher = new FileSystemWatcher(_folderPath, "*.xml")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false
            };

            _watcher.Created += OnFileCreatedOrChanged;
            _watcher.Changed += OnFileCreatedOrChanged;
            _watcher.EnableRaisingEvents = true;
        }

        // 🔥 NEW: PROCESS 10K FILES CŨ
        private Task ProcessExistingFilesAsync()
        {
            try
            {
                var xmlFiles = Directory.GetFiles(_folderPath, "*.xml");
                _logger.LogInformation("📁 Found {Count} existing XML files", xmlFiles.Length);

                int addedCount = 0;

                foreach (var file in xmlFiles)
                {
                    if (_processedFiles.Add(file))
                    {
                        _fileQueue.Add(file);
                        addedCount++;

                        if (addedCount % 1000 == 0)
                        {
                            _logger.LogInformation("📦 Added {Count} files to queue", addedCount);
                        }
                    }
                }

                _logger.LogInformation("✅ Queued {Count} existing files for processing", addedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading existing files");
            }

            return Task.CompletedTask;
        }

        private async void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                await Task.Delay(1000).ConfigureAwait(false);
                if (!File.Exists(e.FullPath)) return;

                if (_processedFiles.Add(e.FullPath))
                {
                    _fileQueue.Add(e.FullPath);
                    _logger.LogInformation($"📁 NEW: Queued {Path.GetFileName(e.FullPath)}");
                }
            }
            catch { }
        }

        private void ProcessQueue(CancellationToken token)
        {
            int processedCount = 0;
            foreach (var filePath in _fileQueue.GetConsumingEnumerable(token))
            {
                try
                {
                    if (!File.Exists(filePath)) continue;

                    _handleXML.AnalysXML130(filePath);
                    RetryDelete(filePath);

                    processedCount++;
                    if (processedCount % 100 == 0)
                    {
                        _logger.LogInformation("⚡ Processed {Count} files", processedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Error processing file: {filePath}");
                }
            }
        }
  
        private void RetryDelete(string filePath)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    File.Delete(filePath);
                    return;
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }
        }

        public int GetQueueCount() => _fileQueue.Count;

        public async Task StopAsync()
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;
            }

            _cts.Cancel();

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            _fileQueue.CompleteAdding();
            await Task.Delay(500);
            _processedFiles.Clear();

            _logger.LogInformation("🛑 STOPPED - Processed {_processedFiles.Count} files total",
                _processedFiles.Count);
        }

        public void Dispose()
        {
            StopAsync().Wait();
            _cts.Dispose();
        }
    }
}