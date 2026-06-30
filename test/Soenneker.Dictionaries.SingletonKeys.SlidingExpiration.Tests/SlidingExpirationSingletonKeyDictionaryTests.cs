using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Dictionaries.SingletonKeys.SlidingExpiration.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class SlidingExpirationSingletonKeyDictionaryTests : HostedUnitTest
{
    public SlidingExpirationSingletonKeyDictionaryTests(Host host) : base(host)
    {
    }

    [Test]
    public async Task Get_reuses_value_before_sliding_expiration()
    {
        var calls = 0;
        var dict = new SlidingExpirationSingletonKeyDictionary<string, string>(TimeSpan.FromMilliseconds(200), key =>
        {
            Interlocked.Increment(ref calls);
            return $"value-{key}";
        });

        string first = await dict.Get("a");
        await Task.Delay(50);
        string second = await dict.Get("a");

        first.Should().Be("value-a");
        second.Should().BeSameAs(first);
        calls.Should().Be(1);

        await dict.DisposeAsync();
    }

    [Test]
    public async Task Get_resets_sliding_expiration()
    {
        var disposed = 0;

        var dict = new SlidingExpirationSingletonKeyDictionary<string, DisposableValue>(TimeSpan.FromMilliseconds(140),
            key => new DisposableValue(() => Interlocked.Increment(ref disposed)));

        DisposableValue first = await dict.Get("a");

        await Task.Delay(90);
        DisposableValue second = await dict.Get("a");

        second.Should().BeSameAs(first);

        await Task.Delay(90);
        dict.TryGet("a", out DisposableValue? stillCached).Should().BeTrue();
        stillCached.Should().BeSameAs(first);
        disposed.Should().Be(0);

        await Task.Delay(220);
        disposed.Should().Be(1);
        dict.TryGet("a", out _).Should().BeFalse();

        await dict.DisposeAsync();
    }

    [Test]
    public async Task Expiration_disposes_value_and_next_get_recreates()
    {
        var calls = 0;
        var disposed = 0;

        var dict = new SlidingExpirationSingletonKeyDictionary<string, DisposableValue>(TimeSpan.FromMilliseconds(60),
            key =>
            {
                Interlocked.Increment(ref calls);
                return new DisposableValue(() => Interlocked.Increment(ref disposed));
            });

        DisposableValue first = await dict.Get("a");

        await Task.Delay(180);

        disposed.Should().Be(1);
        dict.TryGet("a", out _).Should().BeFalse();

        DisposableValue second = await dict.Get("a");

        second.Should().NotBeSameAs(first);
        calls.Should().Be(2);

        await dict.DisposeAsync();
    }

    [Test]
    public async Task Remove_cancels_expiration_timer()
    {
        var disposed = 0;

        var dict = new SlidingExpirationSingletonKeyDictionary<string, DisposableValue>(TimeSpan.FromMilliseconds(60),
            key => new DisposableValue(() => Interlocked.Increment(ref disposed)));

        _ = await dict.Get("a");

        bool removed = await dict.Remove("a");

        removed.Should().BeTrue();
        disposed.Should().Be(1);

        await Task.Delay(150);

        disposed.Should().Be(1);

        await dict.DisposeAsync();
    }

    [Test]
    public async Task Different_keys_initialize_concurrently()
    {
        var started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var dict = new SlidingExpirationSingletonKeyDictionary<string, string>(TimeSpan.FromSeconds(5), async key =>
        {
            if (Interlocked.Increment(ref started) == 2)
                bothStarted.TrySetResult();

            await bothStarted.Task;
            await release.Task;
            return key;
        });

        Task<string> first = Get("a");
        Task<string> second = Get("b");

        bool bothFactoriesStarted = await Task.WhenAny(bothStarted.Task, Task.Delay(TimeSpan.FromSeconds(1))) ==
                                    bothStarted.Task;

        bothStarted.TrySetResult();
        release.TrySetResult();

        bothFactoriesStarted.Should().BeTrue();
        (await Task.WhenAll(first, second)).Should().BeEquivalentTo(["a", "b"]);

        await dict.DisposeAsync();
        return;

        async Task<string> Get(string key)
        {
            return await dict.Get(key);
        }
    }
}