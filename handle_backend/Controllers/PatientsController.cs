using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using handle_backend.Services;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private static FileSystemWatcher _watcher = null!;
    private static readonly BlockingCollection<string> _fileQueue = new();
    private static readonly CancellationTokenSource _cts = new();
    private static readonly object _lock = new();
    private readonly string folderPath = @"D:\XML_Data\QuyetDinh_4750_2023_HSKCB";
    private readonly HandleXML _handleXML;
    // --- Constructor ---
    public PatientsController(HandleXML handleXML)
    {
        _handleXML = handleXML;
        if (_watcher == null)
        {
            // Khởi tạo watcher 1 lần duy nhất
            _watcher = new FileSystemWatcher(folderPath, "*.xml")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false
            };

            _watcher.Created += OnFileCreatedOrChanged;
            _watcher.Changed += OnFileCreatedOrChanged;
            _watcher.EnableRaisingEvents = true;

            // ✅ Khi khởi động: xử lý tất cả file sẵn có
            foreach (var file in Directory.GetFiles(folderPath, "*.xml"))
            {
                _fileQueue.Add(file);
            }

            // ✅ Bắt đầu background task xử lý file trong hàng đợi
            Task.Run(() => ProcessQueue(_cts.Token));
        }
    }

    // --- Sự kiện khi có file mới hoặc bị ghi đè ---
    private async void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Chờ file được ghi xong (tránh đọc khi đang ghi)
            await Task.Delay(500);

            if (System.IO.File.Exists(e.FullPath))
            {
                _fileQueue.Add(e.FullPath);
            }
        }
        catch
        {
            // Bỏ qua lỗi nhỏ khi file đang bị lock hoặc ghi chưa xong
        }
    }

    // --- Xử lý các file trong hàng đợi ---
    private void ProcessQueue(CancellationToken token)
    {
        foreach (var filePath in _fileQueue.GetConsumingEnumerable(token))
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                    continue;
                lock (_lock)
                {
                   
                    // Gọi hàm xử lý XML ở đây
                    _handleXML.AnalysXML130(filePath);
                }
                
                // Xóa sau khi xử lý xong
               // System.IO.File.Delete(filePath);


                Console.WriteLine($"Đã xử lý & xóa file: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi xử lý file {filePath}: {ex.Message}");
            }
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var files = Directory.GetFiles(folderPath, "*.xml").Length;
        return Ok(new
        {
            message = "🩺 Hệ thống theo dõi XML đang chạy",
            pendingFiles = files,
            queueCount = _fileQueue.Count
        });
    }

    // --- Dừng hệ thống (nếu cần) ---
    [HttpPost("stop")]
    public IActionResult Stop()
    {
        _cts.Cancel();
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _fileQueue.CompleteAdding();
        return Ok(new { message = "🛑 Đã dừng theo dõi folder." });
    }
}
