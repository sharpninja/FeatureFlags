using Xunit;

namespace SharpNinja.FeatureFlags.Cqrs.Tests;

/// <summary>TEST-CQRS-MCPSERVER-001: Tests for <see cref="Result{T}"/> and <see cref="Result"/>.</summary>
public class ResultTests
{
    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void SuccessHasValueAndIsSuccess()
    {
        var result = Result.Success(42);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
        Assert.Null(result.Exception);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void FailureWithMessageHasError()
    {
        var result = Result.Failure<int>("bad input");
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal("bad input", result.Error);
        Assert.Null(result.Exception);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void FailureWithExceptionHasBoth()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result.Failure<int>(ex);
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error);
        Assert.Same(ex, result.Exception);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void FailureWithMessageAndException()
    {
        var ex = new InvalidOperationException("inner");
        var result = Result.Failure<int>("outer", ex);
        Assert.Equal("outer", result.Error);
        Assert.Same(ex, result.Exception);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void BindSuccessChains()
    {
        var result = Result.Success(10)
            .Bind(v => Result.Success($"value={v}"));
        Assert.True(result.IsSuccess);
        Assert.Equal("value=10", result.Value);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void BindFailurePropagates()
    {
        var result = Result.Failure<int>("err")
            .Bind(v => Result.Success($"value={v}"));
        Assert.True(result.IsFailure);
        Assert.Equal("err", result.Error);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void MapSuccessTransforms()
    {
        var result = Result.Success(5).Map(v => v * 2);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void MapFailurePropagates()
    {
        var result = Result.Failure<int>("err").Map(v => v * 2);
        Assert.True(result.IsFailure);
        Assert.Equal("err", result.Error);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void GetValueOrDefaultSuccessReturnsValue()
    {
        var result = Result.Success(42);
        Assert.Equal(42, result.GetValueOrDefault(0));
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void GetValueOrDefaultFailureReturnsFallback()
    {
        var result = Result.Failure<int>("err");
        Assert.Equal(-1, result.GetValueOrDefault(-1));
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void ToStringSuccessFormat()
    {
        var result = Result.Success(42);
        var text = result.ToString();
        Assert.StartsWith("Success", text, StringComparison.Ordinal);
        Assert.Contains("42", text, StringComparison.Ordinal);
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void ToStringFailureFormat()
    {
        var result = Result.Failure<int>("oops");
        var text = result.ToString();
        Assert.StartsWith("Failure", text, StringComparison.Ordinal);
        Assert.Contains("oops", text, StringComparison.Ordinal);
    }

    // Non-generic Result tests
    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void NonGenericSuccess()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("Success", result.ToString());
    }

    /// <summary>TEST-CQRS-MCPSERVER-001: Carries over the matching McpServer CQRS unit test scenario.</summary>
    [Fact]
    public void NonGenericFailure()
    {
        var result = Result.Failure("err");
        Assert.True(result.IsFailure);
        Assert.Equal("err", result.Error);
    }
}
