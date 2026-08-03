using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Models;
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

    //[HttpGet]
    //public async Task<IActionResult> GetHistory()
    //{
    //    var data =
    //        await _context.AuditHistories
    //            .OrderByDescending(x => x.CreatedAt)
    //            .ToListAsync();

    //    return Ok(data);
    //}

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAudit(int id)
    {
        var audit = await _context.AuditHistories.FindAsync(id);

        if (audit == null)
        {
            return NotFound(new
            {
                Success = false,
                Message = "Audit not found."
            });
        }

        _context.AuditHistories.Remove(audit);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message = "Audit deleted successfully."
        });
    }


[HttpGet]
public async Task<IActionResult> GetHistory(
    int page = 1,
    int pageSize = 10)
{
    var totalRecords =
        await _context.AuditHistories.CountAsync();

    var audits =
        await _context.AuditHistories
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    var response = new PagedResponseDto<AuditHistory>
    {
        Page = page,
        PageSize = pageSize,
        TotalRecords = totalRecords,
        TotalPages = (int)Math.Ceiling(
            totalRecords / (double)pageSize),

        Items = audits
    };

    return Ok(response);
}
}