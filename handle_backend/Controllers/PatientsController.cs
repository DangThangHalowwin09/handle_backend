using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Xml.Linq;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private static FileInfo _latestFile;   // lưu file mới nhất
    private static readonly object _lock = new(); // tránh race condition
    private static FileSystemWatcher _watcher; // watcher theo dõi folder có file mới không

    private readonly string folderPath = @"D:\XML_Data\QuyetDinh_4750_2023_HSKCB"; // đường dẫn tới folder chứa file XML

    // constructor: khởi tạo chỉ có duy nhất một watcher 
    public PatientsController()
    {
        if (_watcher == null)
        {
            _watcher = new FileSystemWatcher(folderPath, "*.xml");
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _watcher.Created += OnChanged;

            _watcher.Changed += OnChanged;
            _watcher.EnableRaisingEvents = true;

            // khi start lần đầu → quét lấy file mới nhất sẵn
            LoadLatestFile();
        }
    }
    // hàm kiểm tra có file mới không
    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            try
            {
                // chờ file ghi xong
                System.Threading.Thread.Sleep(500);
                var fi = new FileInfo(e.FullPath);

                // cập nhật nếu đây là file mới hơn
                if (_latestFile == null || fi.LastWriteTime > _latestFile.LastWriteTime)
                {
                    _latestFile = fi;
                }
            }
            catch { /* bỏ qua lỗi file đang bị lock */ }
        }
    }

    private void LoadLatestFile()
    {
        var files = Directory.GetFiles(folderPath, "*.xml");
        if (files.Length > 0)
        {
            _latestFile = files
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .First();
        }
    }

    [HttpGet("latest")]
    public IActionResult GetLatest()
    {
        lock (_lock)
        {
            if (_latestFile == null)
               return  NotFound(new { error = "❌ Chưa có file XML nào" });

            try
            {
                XDocument doc = XDocument.Load(_latestFile.FullName);

                XNamespace ns = doc.Root?.GetDefaultNamespace() ?? "";
                var tongHop = doc.Descendants("TONG_HOP").FirstOrDefault()
                             ?? doc.Descendants(ns + "TONG_HOP").FirstOrDefault();

                if (tongHop == null)
                    return  BadRequest(new { error = "❌ Không tìm thấy <TONG_HOP> trong file XML" });

                var patient = new
                {
                    MaBN = (string)(tongHop.Element("MA_BN") ?? tongHop.Element(ns + "MA_BN")) ?? "N/A",
                    HoTenBN = (string)(tongHop.Element("HO_TEN") ?? tongHop.Element(ns + "HO_TEN")) ?? "N/A",
                    NgaySinh = (string)(tongHop.Element("NGAY_SINH") ?? tongHop.Element(ns + "NGAY_SINH")) ?? "N/A"
                };

                return  Ok(new
                {
                    filename = _latestFile.Name,
                    lastModified = _latestFile.LastWriteTime,
                    patient
                });
            }
            catch (IOException ioEx)
            {
                return  StatusCode(500, new { error = $"❌ Lỗi IO: {ioEx.Message}" });
            }
            catch (System.Exception ex)


            {
                return  StatusCode(500, new { error = $"❌ Lỗi xử lý: {ex.Message}" });
            }
        }
    }
}
