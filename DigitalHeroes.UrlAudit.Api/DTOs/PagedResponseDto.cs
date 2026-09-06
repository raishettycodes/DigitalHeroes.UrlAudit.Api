namespace DigitalHeroes.UrlAudit.Api.DTOs
{
    public class PagedResponseDto<T>
    {
        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalRecords { get; set; }

        public int TotalPages { get; set; }

        public List<T> Items { get; set; } = new();
    }
}