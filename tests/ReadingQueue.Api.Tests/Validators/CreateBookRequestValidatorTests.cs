using FluentAssertions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Validators;

namespace ReadingQueue.Api.Tests.Validators;

public class CreateBookRequestValidatorTests
{
    private readonly CreateBookRequestValidator _sut = new();

    private static CreateBookRequest Valid() => new(
        Title:            "Cien años de soledad",
        Author:           "Gabriel Garcia Marquez",
        Genre:            "Clasico",
        Subgenre:         "",
        Country:          "Colombia",
        WhyRead:          null,
        Priority:         3,
        MentalEnergy:     "Baja - cualquier momento",
        RecommendedMood:  "Analitico / quiero aprender algo",
        RotationCategory: "Clasico",
        Notes:            null
    );

    [Fact]
    public void Validate_AllFieldsValid_IsValid()
    {
        var result = _sut.Validate(Valid());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyTitle_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { Title = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_EmptyAuthor_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { Author = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Author");
    }

    [Fact]
    public void Validate_EmptyGenre_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { Genre = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Genre");
    }

    [Fact]
    public void Validate_EmptyCountry_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { Country = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Country");
    }

    [Fact]
    public void Validate_EmptyMentalEnergy_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { MentalEnergy = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MentalEnergy");
    }

    [Fact]
    public void Validate_EmptyRecommendedMood_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { RecommendedMood = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RecommendedMood");
    }

    [Fact]
    public void Validate_EmptyRotationCategory_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { RotationCategory = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RotationCategory");
    }

    [Fact]
    public void Validate_PriorityZero_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { Priority = 0 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Priority");
    }

    [Fact]
    public void Validate_PrioritySix_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { Priority = 6 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Priority");
    }

    [Fact]
    public void Validate_PriorityThree_IsValid()
    {
        var result = _sut.Validate(Valid() with { Priority = 3 });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhyReadTooLong_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { WhyRead = new string('x', 1001) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "WhyRead");
    }

    [Fact]
    public void Validate_NotesTooLong_IsInvalid()
    {
        var result = _sut.Validate(Valid() with { Notes = new string('x', 2001) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Notes");
    }
}
