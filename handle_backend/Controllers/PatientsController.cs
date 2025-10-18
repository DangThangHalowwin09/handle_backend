using handle_backend.Services;
using handle_backend.Services.XML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IFileWatcherService _watcherService;

    public PatientsController(IFileWatcherService watcherService)
    {
        _watcherService = watcherService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start()
    {
        await _watcherService.StartAsync();
        return Ok("🩺 Started!");
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var files = Directory.GetFiles(@"D:\XML_Data\QuyetDinh_4750_2023_HSKCB", "*.xml").Length;
        return Ok(new
        {
            message = "🩺 System running",
            pendingFiles = files,
            queueCount = _watcherService.GetQueueCount(),
            isRunning = _watcherService.IsRunning
        });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await _watcherService.StopAsync();
        return Ok("🛑 Stopped!");
    }
}
