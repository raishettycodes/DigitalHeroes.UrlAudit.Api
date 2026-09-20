using DigitalHeroes.UrlAudit.Api.Helpers;

namespace DigitalHeroes.UrlAudit.Tests;

public class SeoScoreCalculatorTests
{
    [Fact]
    public void Calculate_AllCriteriaSatisfied_Returns100()
    {
        var score = SeoScoreCalculator.Calculate(
            title: "Page Title",
            metaDescription: "Page description",
            h1: 1,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: true);

        Assert.Equal(100, score);
    }

    [Fact]
    public void Calculate_NoCriteriaSatisfied_ReturnsZero()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 0,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(20, score);
    }

    [Fact]
    public void Calculate_TitlePresent_Adds20Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: "Page Title",
            metaDescription: null,
            h1: 0,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(40, score);
    }

    [Fact]
    public void Calculate_MetaDescriptionPresent_Adds20Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: "Description",
            h1: 0,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(40, score);
    }

    [Fact]
    public void Calculate_ExactlyOneH1_Adds20Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 1,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(40, score);
    }

    [Fact]
    public void Calculate_MultipleH1s_AddsNoH1Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 2,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(20, score);
    }

    [Fact]
    public void Calculate_NoImages_Adds20Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 0,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(20, score);
    }

    [Fact]
    public void Calculate_AllImagesHaveAlt_Adds20Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 0,
            images: 4,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(20, score);
    }

    [Fact]
    public void Calculate_HalfImagesHaveAlt_Adds10Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 0,
            images: 4,
            imagesWithoutAlt: 2,
            ssl: false);

        Assert.Equal(10, score);
    }

    [Fact]
    public void Calculate_NoImagesHaveAlt_AddsZeroImagePoints()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 0,
            images: 4,
            imagesWithoutAlt: 4,
            ssl: false);

        Assert.Equal(0, score);
    }

    [Fact]
    public void Calculate_SslEnabled_Adds20Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 0,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: true);

        Assert.Equal(40, score);
    }

    [Fact]
    public void Calculate_WhitespaceTitleAndDescription_DoNotAddPoints()
    {
        var score = SeoScoreCalculator.Calculate(
            title: "   ",
            metaDescription: "\t",
            h1: 0,
            images: 0,
            imagesWithoutAlt: 0,
            ssl: false);

        Assert.Equal(20, score);
    }

    [Fact]
    public void Calculate_OneOfThreeImagesMissingAlt_Adds13Points()
    {
        var score = SeoScoreCalculator.Calculate(
            title: null,
            metaDescription: null,
            h1: 0,
            images: 3,
            imagesWithoutAlt: 1,
            ssl: false);

        Assert.Equal(13, score);
    }
}
