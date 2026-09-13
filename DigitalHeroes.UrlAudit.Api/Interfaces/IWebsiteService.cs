using DigitalHeroes.UrlAudit.Api.DTOs.Website;

namespace DigitalHeroes.UrlAudit.Api.Interfaces
{
    public interface IWebsiteService
    {
        Task<WebsiteDto> AddAsync(
            int userId,
            CreateWebsiteDto dto);

        Task<List<WebsiteDto>> GetAllAsync(
            int userId);

        Task<WebsiteDto?> GetByIdAsync(
            int userId,
            int id);

        Task<WebsiteDto?> UpdateAsync(
            int userId,
            int id,
            UpdateWebsiteDto dto);

        Task DeleteAsync(
            int userId,
            int id);
    }
}