using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeeSight.Gateway.RateLimiting;
using StackExchange.Redis;

namespace SeeSight.Gateway.Tests;

public sealed class RedisRateLimitMiddlewareTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly RateLimitOptions _options = new() { RequestsPerWindow = 2, WindowSeconds = 60 };

    public RedisRateLimitMiddlewareTests()
    {
        _redis.GetDatabase().Returns(_database);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private (RedisRateLimitMiddleware Middleware, Func<bool> WasNextCalled) CreateMiddleware()
    {
        var nextCalled = false;
        var middleware = new RedisRateLimitMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            _redis,
            Options.Create(_options),
            NullLogger<RedisRateLimitMiddleware>.Instance);
        return (middleware, () => nextCalled);
    }

    [Fact]
    public async Task Requests_to_unlimited_paths_pass_through_without_touching_redis()
    {
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("GET", "/trips");

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
        _redis.DidNotReceive().GetDatabase();
    }

    [Fact]
    public async Task GET_requests_to_a_limited_path_are_not_rate_limited()
    {
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("GET", "/auth/login");

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
        _redis.DidNotReceive().GetDatabase();
    }

    [Fact]
    public async Task Requests_under_the_limit_pass_through_and_set_expiry_on_the_first_request()
    {
        _database.StringIncrementAsync(Arg.Any<RedisKey>()).Returns(1L);
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("POST", "/auth/login");

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        await _database.Received(1).KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Requests_beyond_the_window_limit_are_rejected_with_429()
    {
        _database.StringIncrementAsync(Arg.Any<RedisKey>()).Returns(3L);
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("POST", "/auth/login");

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Headers.RetryAfter.ToString().Should().Be(_options.WindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Redis_errors_fail_open_and_allow_the_request_through()
    {
        _database.StringIncrementAsync(Arg.Any<RedisKey>()).ThrowsAsync(new TimeoutException("redis unreachable"));
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("POST", "/auth/login");

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Redis_errors_increment_the_fail_open_metric()
    {
        _database.StringIncrementAsync(Arg.Any<RedisKey>()).ThrowsAsync(new TimeoutException("redis unreachable"));

        var measurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "SeeSight.Gateway.RateLimiting")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => measurements.Add(measurement));
        listener.Start();

        var (middleware, _) = CreateMiddleware();
        var context = CreateContext("POST", "/auth/login");

        await middleware.InvokeAsync(context);

        measurements.Should().ContainSingle().Which.Should().Be(1);
    }
}
