using FluentAssertions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Validators;

namespace ReadingQueue.Api.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _sut = new();

    [Fact]
    public void Validate_AllFieldsValid_IsValid()
    {
        var result = _sut.Validate(new RegisterRequest(
            "user@example.com", "Password1", "Test User"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PasswordTooShort_IsInvalid()
    {
        var result = _sut.Validate(new RegisterRequest(
            "user@example.com", "Pass1", "Test User"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_PasswordNoUppercase_IsInvalidWithDescriptiveMessage()
    {
        var result = _sut.Validate(new RegisterRequest(
            "user@example.com", "password1", "Test User"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Password" &&
            e.ErrorMessage.Contains("mayúscula"));
    }

    [Fact]
    public void Validate_PasswordNoNumber_IsInvalidWithDescriptiveMessage()
    {
        var result = _sut.Validate(new RegisterRequest(
            "user@example.com", "PasswordOnly", "Test User"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Password" &&
            e.ErrorMessage.Contains("número"));
    }

    [Fact]
    public void Validate_InvalidEmail_IsInvalid()
    {
        var result = _sut.Validate(new RegisterRequest(
            "not-an-email", "Password1", "Test User"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_EmptyDisplayName_IsInvalid()
    {
        var result = _sut.Validate(new RegisterRequest(
            "user@example.com", "Password1", ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayName");
    }
}
