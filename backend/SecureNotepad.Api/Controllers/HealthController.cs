using Microsoft.AspNetCore.Mvc;
using SecureNotepad.Api.Services;

namespace SecureNotepad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly INoteRepository _repository;

    public HealthController(INoteRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var dbAvailable = await _repository.PingAsync(cancellationToken);

        return Ok(new
        {
            status = dbAvailable ? "Healthy" : "Degraded",
            database = dbAvailable ? "Connected" : "Disconnected",
            service = "PrivatePad API",
            timestamp = DateTime.UtcNow
        });
    }
}

