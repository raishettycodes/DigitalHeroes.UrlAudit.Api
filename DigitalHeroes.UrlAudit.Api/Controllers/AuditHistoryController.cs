using DigitalHeroes.UrlAudit.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditHistoryController : ControllerBase
{
    private readonly UrlAuditDbContext _context;

    public AuditHistoryController(
        UrlAuditDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var data =
            await _context.AuditHistories
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

        return Ok(data);
    }
}