using DigitalHeroes.UrlAudit.Api.Services;

namespace DigitalHeroes.UrlAudit.Tests;

public class SeoAuditServiceTests
{
    private readonly SeoAuditService _service = new();

    [Fact]
    public void Analyze_ExtractsTitleAndMetaDescription()
    {
        const string html = """
            <html>
                <head>
                    <title>DigitalHeroes URL Audit</title>
                    <meta name="description" content="Audit your website SEO.">
                </head>
            </html>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(
            "DigitalHeroes URL Audit",
            result.Title);

        Assert.Equal(
            "Audit your website SEO.",
            result.MetaDescription);
    }

    [Fact]
    public void Analyze_HandlesUppercaseMetaDescriptionName()
    {
        const string html = """
            <html>
                <head>
                    <meta NAME="DESCRIPTION" content="Uppercase description">
                </head>
            </html>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(
            "Uppercase description",
            result.MetaDescription);
    }

    [Fact]
    public void Analyze_CountsH1AndH2Elements()
    {
        const string html = """
            <h1>Main heading</h1>
            <h1>Second main heading</h1>
            <h2>Section one</h2>
            <h2>Section two</h2>
            <h2>Section three</h2>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(2, result.H1Count);
        Assert.Equal(3, result.H2Count);
    }

    [Fact]
    public void Analyze_CountsImagesAndImagesWithoutAlt()
    {
        const string html = """
            <img src="one.jpg" alt="First image">
            <img src="two.jpg" alt="">
            <img src="three.jpg">
            <img src="four.jpg" alt="Fourth image">
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(4, result.Images);
        Assert.Equal(2, result.ImagesWithoutAlt);
    }

    [Fact]
    public void Analyze_CountsInternalAndExternalLinks()
    {
        const string html = """
            <a href="/about">About</a>
            <a href="https://example.com/contact">Contact</a>
            <a href="https://google.com">Google</a>
            <a href="https://github.com">GitHub</a>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(2, result.InternalLinks);
        Assert.Equal(2, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_CountsRelativeLinksAsInternal()
    {
        const string html = """
            <a href="/about">About</a>
            <a href="products/item">Product</a>
            <a href="../contact">Contact</a>
            """;

        var result = _service.Analyze(
            "https://example.com/products/page",
            html);

        Assert.Equal(3, result.InternalLinks);
        Assert.Equal(0, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_IgnoresNonWebLinks()
    {
        const string html = """
            <a href="#section">Section</a>
            <a href="mailto:test@example.com">Email</a>
            <a href="tel:+123456789">Phone</a>
            <a href="javascript:void(0)">JavaScript</a>
            <a href="data:text/plain,hello">Data</a>
            <a href="ftp://example.com/file.txt">FTP</a>
            <a href="/valid">Valid</a>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(1, result.InternalLinks);
        Assert.Equal(0, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_HandlesLinksWithWhitespace()
    {
        const string html = """
            <a href="  /about  ">About</a>
            <a href="  https://google.com  ">Google</a>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(1, result.InternalLinks);
        Assert.Equal(1, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_HandlesInvalidBaseUrl()
    {
        const string html = """
            <a href="/about">About</a>
            <a href="https://google.com">Google</a>
            """;

        var result = _service.Analyze(
            "not-a-valid-url",
            html);

        Assert.Equal(0, result.InternalLinks);
        Assert.Equal(0, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_WhenNoImagesOrLinks_ReturnsZeroCounts()
    {
        const string html = """
            <html>
                <head>
                    <title>Simple Page</title>
                </head>
                <body>
                    <h1>Hello</h1>
                    <p>No images or links.</p>
                </body>
            </html>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(0, result.Images);
        Assert.Equal(0, result.ImagesWithoutAlt);
        Assert.Equal(0, result.InternalLinks);
        Assert.Equal(0, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_WhenElementsAreMissing_ReturnsDefaultValues()
    {
        const string html = """
            <html>
                <body>
                    <p>Content only.</p>
                </body>
            </html>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Null(result.Title);
        Assert.Null(result.MetaDescription);
        Assert.Equal(0, result.H1Count);
        Assert.Equal(0, result.H2Count);
        Assert.Equal(0, result.Images);
        Assert.Equal(0, result.ImagesWithoutAlt);
        Assert.Equal(0, result.InternalLinks);
        Assert.Equal(0, result.ExternalLinks);
    }

    [Fact]
    public void Analyze_DoesNotCalculateSeoScore()
    {
        const string html = """
            <title>Good Page</title>
            <meta name="description" content="Good description">
            <h1>Main heading</h1>
            """;

        var result = _service.Analyze(
            "https://example.com",
            html);

        Assert.Equal(0, result.SeoScore);
    }
}
