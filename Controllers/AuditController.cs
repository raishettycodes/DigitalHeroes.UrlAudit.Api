using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DigitalHeroes.UrlAudit.Api.Controllers
{
    /// <summary>
    /// API used to audit website URLs.
    /// </summary>
    [EnableRateLimiting("FixedPolicy")]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly AuditService _auditService;

        public AuditController(AuditService auditService)
        {
            _auditService = auditService;
        }

        /// <summary>
        /// Audits a URL and returns availability,
        /// response time,
        /// HTTP status code,
        /// and reachability.
        /// </summary>
        /// <param name="request">
        /// URL audit request.
        /// </param>
        /// <returns>
        /// Audit result.
        /// </returns>


        [ProducesResponseType(typeof(AuditResponseDto),StatusCodes.Status200OK)]

        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        [HttpPost]
        public async Task<IActionResult> Audit([FromBody] AuditRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _auditService.AuditUrlAsync(request.Url);

            return Ok(result);
        }
    }
}