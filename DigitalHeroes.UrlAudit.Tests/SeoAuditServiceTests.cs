using DigitalHeroes.UrlAudit.Api.Services;

namespace DigitalHeroes.UrlAudit.Tests;

public class SeoAuditServiceTests
{
    [Fact]
    public void Analyze_ExtractsSeoElementsCorrectly()
    {
        var service = new SeoAuditService();

        var html = """
            <html>
            <head>
                <title>Test Website</title>
                <meta name="description" content="A test website description">
            </head>
            <body>
                <h1>Main Heading</h1>
                <h1>Second Heading</h1>
                <h2>Sub Heading</h2>
                <img src="image1.jpg" alt="Logo">
                <img src="image2.jpg">
                <a href="/about">About</a>
                <a href="https://example.org">External</a>
            </body>
            </html>
            """;

        var result = service.Analyze(
            "https://example.com",
            html);

        Assert.Equal("Test Website", result.Title);
        Assert.Equal(
            "A test website description",
            result.MetaDescription);

        Assert.Equal(2, result.H1Count);
        Assert.Equal(1, result.H2Count);

        Assert.Equal(2, result.Images);
        Assert.Equal(1, result.ImagesWithoutAlt);

        Assert.Equal(1, result.InternalLinks);
        Assert.Equal(1, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_IgnoresNonWebLinks()
    {
        var service = new SeoAuditService();

        var html = """
            <html>
            <body>
                <a href="#section">Section</a>
                <a href="mailto:test@example.com">Email</a>
                <a href="tel:1234567890">Phone</a>
                <a href="javascript:void(0)">JavaScript</a>
                <a href="https://example.com/page">Internal</a>
            </body>
            </html>
            """;

        var result = service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(1, result.InternalLinks);
        Assert.Equal(0, result.ExternalLinks);
    }
}
