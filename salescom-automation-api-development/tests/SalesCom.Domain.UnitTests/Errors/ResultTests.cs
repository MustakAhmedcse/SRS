namespace SalesCom.Domain.UnitTests.Errors;

using SalesCom.Domain.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_NonGeneric_IsSuccess()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(ErrorBase.None);
    }

    [Fact]
    public void Failure_NonGeneric_HoldsError()
    {
        var error = ErrorBase.Validation("Test.Bad", "no good");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void GenericResult_AccessingValueOnFailure_Throws()
    {
        var result = Result.Failure<int>(ErrorBase.Validation("X.Y", "nope"));

        var act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GenericResult_SuccessReturnsValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversionFromValue_ProducesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversionFromError_ProducesFailure()
    {
        var error = ErrorBase.NotFound("X.Y", "missing");

        Result<string> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }
}
