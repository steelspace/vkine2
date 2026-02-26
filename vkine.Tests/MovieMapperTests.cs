using Moq;
using vkine.Mappers;
using vkine.Models;
using vkine.Services;

namespace vkine.Tests;

public class MovieMapperTests
{
    private readonly Mock<ICountryLookupService> _lookupMock;
    private readonly MovieMapper _mapper;

    public MovieMapperTests()
    {
        _lookupMock = new Mock<ICountryLookupService>();
        _lookupMock.Setup(s => s.GetCountryNames(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(codes => codes.Select(c => c).ToList());
        _mapper = new MovieMapper(_lookupMock.Object);
    }

    private static MovieDocument CreateDoc() => new()
    {
        CsfdId = 1,
        Title = "Test"
    };

    // ── OriginCountryCodes branch ────────────────────────────

    [Fact]
    public void OriginCountryCodes_ValidCodes_ReturnsTrimmed()
    {
        var doc = CreateDoc();
        doc.OriginCountryCodes = ["US", " CZ ", "DE"];

        var result = _mapper.Map(doc);

        Assert.Equal(["US", "CZ", "DE"], result.OriginCountryCodes);
    }

    [Fact]
    public void OriginCountryCodes_IgnoresWhitespaceEntries()
    {
        var doc = CreateDoc();
        doc.OriginCountryCodes = ["US", "", "  ", null!, "CZ"];

        var result = _mapper.Map(doc);

        Assert.Equal(["US", "CZ"], result.OriginCountryCodes);
    }

    [Fact]
    public void OriginCountryCodes_DeduplicatesCaseInsensitive()
    {
        var doc = CreateDoc();
        doc.OriginCountryCodes = ["us", "US", "Us"];

        var result = _mapper.Map(doc);

        Assert.Single(result.OriginCountryCodes);
    }

    [Fact]
    public void OriginCountryCodes_EmptyList_FallsToOrigin()
    {
        _lookupMock.Setup(s => s.GetIsoCodeFromCzechName("Česko")).Returns("CZ");

        var doc = CreateDoc();
        doc.OriginCountryCodes = [];
        doc.Origin = "Česko";

        var result = _mapper.Map(doc);

        Assert.Equal(["CZ"], result.OriginCountryCodes);
    }

    // ── Origin string branch ─────────────────────────────────

    [Fact]
    public void Origin_TwoLetterCodes_UppercasedDirectly()
    {
        var doc = CreateDoc();
        doc.Origin = "us, de";

        var result = _mapper.Map(doc);

        Assert.Equal(["US", "DE"], result.OriginCountryCodes);
    }

    [Fact]
    public void Origin_CzechNames_LookedUp()
    {
        _lookupMock.Setup(s => s.GetIsoCodeFromCzechName("Česko")).Returns("CZ");
        _lookupMock.Setup(s => s.GetIsoCodeFromCzechName("Německo")).Returns("DE");

        var doc = CreateDoc();
        doc.Origin = "Česko, Německo";

        var result = _mapper.Map(doc);

        Assert.Equal(["CZ", "DE"], result.OriginCountryCodes);
    }

    [Fact]
    public void Origin_SlashSeparated_Parsed()
    {
        var doc = CreateDoc();
        doc.Origin = "US/DE";

        var result = _mapper.Map(doc);

        Assert.Equal(["US", "DE"], result.OriginCountryCodes);
    }

    [Fact]
    public void Origin_UnknownNames_Filtered()
    {
        _lookupMock.Setup(s => s.GetIsoCodeFromCzechName("Unknown")).Returns((string?)null);

        var doc = CreateDoc();
        doc.Origin = "US, Unknown";

        var result = _mapper.Map(doc);

        Assert.Equal(["US"], result.OriginCountryCodes);
    }

    [Fact]
    public void Origin_DeduplicatesCaseInsensitive()
    {
        var doc = CreateDoc();
        doc.Origin = "us, US";

        var result = _mapper.Map(doc);

        Assert.Single(result.OriginCountryCodes);
    }

    // ── No origin data ───────────────────────────────────────

    [Fact]
    public void NullOriginAndCodes_ReturnsEmpty()
    {
        var doc = CreateDoc();
        doc.OriginCountryCodes = null;
        doc.Origin = null;

        var result = _mapper.Map(doc);

        Assert.Empty(result.OriginCountryCodes);
    }

    [Fact]
    public void WhitespaceOrigin_ReturnsEmpty()
    {
        var doc = CreateDoc();
        doc.OriginCountryCodes = null;
        doc.Origin = "   ";

        var result = _mapper.Map(doc);

        Assert.Empty(result.OriginCountryCodes);
    }
}
