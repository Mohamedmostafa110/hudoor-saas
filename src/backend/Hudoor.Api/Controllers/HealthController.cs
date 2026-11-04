using Hudoor.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public HealthController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        bool canConnect = await _dbContext.Database.CanConnectAsync();

        return Ok(new
        {
            status = "Healthy",
            service = "Hudoor.Api",
            version = "1.0.0",
            database = canConnect ? "Connected" : "Disconnected",
            timestamp = DateTime.UtcNow
        });
    }
}
