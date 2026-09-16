using SpiderHood.Models;

namespace SpiderHood.Tests;

public class OperationResultTests
{
    [Fact]
    public void Success_WithoutData_IsSuccessfulAndHasNoErrorOrData()
    {
        var result = OperationResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Success_WithData_CarriesThatData()
    {
        var payload = new { Id = 42 };

        var result = OperationResult.Success(payload);

        Assert.True(result.IsSuccess);
        Assert.Same(payload, result.Data);
    }

    [Fact]
    public void Failure_CarriesErrorMessageAndNoData()
    {
        var result = OperationResult.Failure("algo salió mal");

        Assert.False(result.IsSuccess);
        Assert.Equal("algo salió mal", result.ErrorMessage);
        Assert.Null(result.Data);
    }
}
