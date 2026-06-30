using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Locks;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Dictionaries.SingletonKeys.SlidingExpiration;

internal sealed class SlidingExpirationEntry<TKey, TValue> : IDisposable, IAsyncDisposable where TKey : notnull
{
    public readonly SlidingExpirationSingletonKeyDictionary<TKey, TValue> Owner;
    public readonly TKey Key;
    public readonly AsyncLock Lock;
    public readonly Timer Timer;

    public TValue? Value;
    public bool HasValue;
    public long ExpiresAtTick;

    public SlidingExpirationEntry(SlidingExpirationSingletonKeyDictionary<TKey, TValue> owner, TKey key)
    {
        Owner = owner;
        Key = key;
        Lock = new AsyncLock();

        Timer = new Timer(static state =>
        {
            var entry = (SlidingExpirationEntry<TKey, TValue>) state!;
            entry.Owner.QueueExpiration(entry);
        }, this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public bool TryTakeValue(out TValue? value)
    {
        if (!HasValue)
        {
            value = default;
            return false;
        }

        value = Value;
        Value = default;
        HasValue = false;
        return true;
    }

    public void DisposeTimer()
    {
        Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Timer.Dispose();
    }

    public async ValueTask DisposeTimerAsync()
    {
        Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        await Timer.DisposeAsync()
                   .NoSync();
    }

    public void Dispose()
    {
        DisposeTimer();
    }

    public ValueTask DisposeAsync()
    {
        return DisposeTimerAsync();
    }
}
