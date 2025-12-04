using Application.Commands;
using Application.DTOs;
using Application.Interfaces.Mediator;
using Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("v1/mxf")]
public sealed class MxfController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MxfController> _logger;
    private readonly IConfiguration _configuration;

    public MxfController(IMediator mediator, ILogger<MxfController> logger, IConfiguration configuration)
    {
        _mediator = mediator;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStatus([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStatusQuery { ProcessId = id }, ct);
        return Ok(result);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<IActionResult> UploadToApi(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("file is required");

        var processId = Guid.NewGuid();

        var tempFolder = _configuration.GetValue<string>("Worker:TempFolder") ?? "/app/temp";

        Directory.CreateDirectory(tempFolder);

        var localPath = Path.Combine(tempFolder, $"{processId}.mxf");

        await using (var fs = System.IO.File.Create(localPath))
        {
            await file.CopyToAsync(fs, ct);
            await fs.FlushAsync(ct);
        }

        var result = await _mediator.Send(new StartUploadCommand
        {
            Id = processId,
            Path = localPath
        }, ct);

        return Accepted(new
        {
            ProcessId = result.ProcessId,
            LocalPath = localPath,
            Status = "queued"
        });
    }
}