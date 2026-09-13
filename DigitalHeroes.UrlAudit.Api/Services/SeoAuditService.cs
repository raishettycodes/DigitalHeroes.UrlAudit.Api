using DigitalHeroes.UrlAudit.Api.DTOs.Audit;
using HtmlAgilityPack;

namespace DigitalHeroes.UrlAudit.Api.Services;

public class SeoAuditService
{
    public SeoAuditDto Analyze(
        string url,
        string html)
    {
        var document = new HtmlDocument();

        document.LoadHtml(html);

        var result = new SeoAuditDto();

        /*
         * ---------------------------------------------------------
         * Title
         * ---------------------------------------------------------
         */

        result.Title =
            document.DocumentNode
                .SelectSingleNode("//title")
                ?.InnerText
                ?.Trim();

        /*
         * ---------------------------------------------------------
         * Meta Description
         * ---------------------------------------------------------
         */

        var metaDescriptionNode =
            document.DocumentNode
                .SelectSingleNode(
                    "//meta[translate(@name, " +
                    "'ABCDEFGHIJKLMNOPQRSTUVWXYZ', " +
                    "'abcdefghijklmnopqrstuvwxyz')=" +
                    "'description']");

        result.MetaDescription =
            metaDescriptionNode
                ?.GetAttributeValue("content", "")
                ?.Trim();

        /*
         * ---------------------------------------------------------
         * H1
         * ---------------------------------------------------------
         */

        result.H1Count =
            document.DocumentNode
                .SelectNodes("//h1")
                ?.Count ?? 0;

        /*
         * ---------------------------------------------------------
         * H2
         * ---------------------------------------------------------
         */

        result.H2Count =
            document.DocumentNode
                .SelectNodes("//h2")
                ?.Count ?? 0;

        /*
         * ---------------------------------------------------------
         * Images
         * ---------------------------------------------------------
         */

        var images =
            document.DocumentNode
                .SelectNodes("//img");

        result.Images =
            images?.Count ?? 0;

        result.ImagesWithoutAlt =
            images?.Count(image =>
                string.IsNullOrWhiteSpace(
                    image.GetAttributeValue("alt", "")))
            ?? 0;

        /*
         * ---------------------------------------------------------
         * Internal / External Links
         * ---------------------------------------------------------
         */

        CountLinks(
            document,
            url,
            out var internalLinks,
            out var externalLinks);

        result.InternalLinks =
            internalLinks;

        result.ExternalLinks =
            externalLinks;

        /*
         * ---------------------------------------------------------
         * SEO Score
         * ---------------------------------------------------------
         *
         * Score is calculated separately by
         * SeoScoreCalculator in AuditService.
         *
         * We leave SeoScore at its default value here.
         * ---------------------------------------------------------
         */

        return result;
    }

    private static void CountLinks(
        HtmlDocument document,
        string baseUrl,
        out int internalLinks,
        out int externalLinks)
    {
        internalLinks = 0;
        externalLinks = 0;

        if (!Uri.TryCreate(
                baseUrl,
                UriKind.Absolute,
                out var baseUri))
        {
            return;
        }

        var links =
            document.DocumentNode
                .SelectNodes("//a[@href]");

        if (links == null)
        {
            return;
        }

        foreach (var link in links)
        {
            var href =
                link.GetAttributeValue(
                    "href",
                    string.Empty)
                ?.Trim();

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            /*
             * Ignore non-web links.
             */

            if (href.StartsWith("#") ||
                href.StartsWith(
                    "mailto:",
                    StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith(
                    "tel:",
                    StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith(
                    "javascript:",
                    StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith(
                    "data:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            /*
             * Remove fragment-only portion while
             * preserving the actual URL.
             */

            if (Uri.TryCreate(
                    baseUri,
                    href,
                    out var linkUri))
            {
                /*
                 * Only count HTTP / HTTPS links.
                 */

                if (linkUri.Scheme != Uri.UriSchemeHttp &&
                    linkUri.Scheme != Uri.UriSchemeHttps)
                {
                    continue;
                }

                /*
                 * Same host = internal.
                 *
                 * Different host = external.
                 */

                if (string.Equals(
                        linkUri.Host,
                        baseUri.Host,
                        StringComparison.OrdinalIgnoreCase))
                {
                    internalLinks++;
                }
                else
                {
                    externalLinks++;
                }
            }
        }
    }
}