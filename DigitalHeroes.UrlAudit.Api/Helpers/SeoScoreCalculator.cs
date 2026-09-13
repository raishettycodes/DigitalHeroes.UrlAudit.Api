namespace DigitalHeroes.UrlAudit.Api.Helpers
{
    public static class SeoScoreCalculator
    {
        public static int Calculate(
            string? title,
            string? metaDescription,
            int h1,
            int images,
            int imagesWithoutAlt,
            bool ssl)
        {
            int score = 0;

            // Title
            if (!string.IsNullOrWhiteSpace(title))
                score += 20;

            // Meta Description
            if (!string.IsNullOrWhiteSpace(metaDescription))
                score += 20;

            // H1
            if (h1 == 1)
                score += 20;

            // Images with Alt
            if (images == 0)
            {
                score += 20;
            }
            else
            {
                double percentage =
                    (double)(images - imagesWithoutAlt) / images;

                score += (int)(percentage * 20);
            }

            // SSL
            if (ssl)
                score += 20;

            return score;
        }
    }
}