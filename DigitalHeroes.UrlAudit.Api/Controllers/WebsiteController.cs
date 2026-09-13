using DigitalHeroes.UrlAudit.Api.DTOs.Website;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WebsiteController : ControllerBase
    {
        private readonly IWebsiteService _websiteService;

        public WebsiteController(IWebsiteService websiteService)
        {
            _websiteService = websiteService;
        }

        private int GetUserId()
        {
            return int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        }

        [HttpPost]
        public async Task<IActionResult> Add(CreateWebsiteDto dto)
        {
            var userId = GetUserId();

            var result =
                await _websiteService.AddAsync(userId, dto);

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();

            return Ok(
                await _websiteService.GetAllAsync(userId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();

            var result =
                await _websiteService.GetByIdAsync(userId, id);

            if (result == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = "Website not found."
                });
            }

            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            int id,
            UpdateWebsiteDto dto)
        {
            var userId = GetUserId();

            var result =
                await _websiteService.UpdateAsync(
                    userId,
                    id,
                    dto);

            if (result == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = "Website not found."
                });
            }

            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();

            var website =
                await _websiteService.GetByIdAsync(
                    userId,
                    id);

            if (website == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = "Website not found."
                });
            }

            await _websiteService.DeleteAsync(
                userId,
                id);

            return Ok(new
            {
                Success = true,
                Message = "Website deleted successfully."
            });
        }
    }
}