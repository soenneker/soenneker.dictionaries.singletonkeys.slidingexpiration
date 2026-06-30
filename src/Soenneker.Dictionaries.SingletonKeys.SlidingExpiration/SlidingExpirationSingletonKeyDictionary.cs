using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Dictionaries.SingletonKeys.SlidingExpiration.Abstract;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Dictionaries.SingletonKeys.SlidingExpiration;

/// <inheritdoc cref="ISlidingExpirationSingletonKeyDictionary{TKey,TValue}"/>
public sealed class SlidingExpirationSingletonKeyDictionary<TKey, TValue> : ISlidingExpirationSingletonKeyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, SlidingExpirationEntry<TKey, TValue>> _entries = new();
    private readonly long _slidingExpirationMilliseconds;

    private Func<TKey, CancellationToken, ValueTask<TValue>>? _factory;
    private int _disposed;

    public TimeSpan SlidingExpiration { get; }

    public SlidingExpirationSingletonKeyDictionary(TimeSpan slidingExpiration)
    {
        ValidateSlidingExpiration(slidingExpiration);

        SlidingExpiration = slidingExpiration;
        _slidingExpirationMilliseconds = Math.Max(1L, (long) Math.Ceiling(slidingExpiration.TotalMilliseconds));
    }

    public SlidingExpirationSingletonKeyDictionary(TimeSpan slidingExpiration, Func<TKey, ValueTask<TValue>> func) : this(slidingExpiration)
    {
        SetInitialization(func);
    }

    public SlidingExpirationSingletonKeyDictionary(TimeSpan slidingExpiration, Func<TKey, CancellationToken, ValueTask<TValue>> func) : this(slidingExpiration)
    {
        SetInitialization(func);
    }

    public SlidingExpirationSingletonKeyDictionary(TimeSpan slidingExpiration, Func<ValueTask<TValue>> func) : this(slidingExpiration)
    {
        SetInitialization(func);
    }

    public SlidingExpirationSingletonKeyDictionary(TimeSpan slidingExpiration, Func<TKey, TValue> func) : this(slidingExpiration)
    {
        SetInitialization(func);
    }

    public SlidingExpirationSingletonKeyDictionary(TimeSpan slidingExpiration, Func<TKey, CancellationToken, TValue> func) : this(slidingExpiration)
    {
        SetInitialization(func);
    }

    public SlidingExpirationSingletonKeyDictionary(TimeSpan slidingExpiration, Func<TValue> func) : this(slidingExpiration)
    {
        SetInitialization(func);
    }

    public async ValueTask<TValue> Get(TKey key, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            ThrowIfDisposed();

            SlidingExpirationEntry<TKey, TValue> entry = GetOrAddEntry(key);

            using (await entry.Lock.Lock(cancellationToken).NoSync())
            {
                ThrowIfDisposed();

                if (!IsCurrentEntry(key, entry))
                    continue;

                await ExpireValueForReuseNoLock(entry).NoSync();

                try
                {
                    TValue value = await GetOrCreateNoLock(entry, key, cancellationToken).NoSync();
                    ResetExpirationNoLock(entry);
                    return value;
                }
                catch
                {
                    RemoveEmptyEntryNoLock(key, entry);
                    throw;
                }
            }
        }
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        value = default;
        ThrowIfDisposed();

        if (!_entries.TryGetValue(key, out SlidingExpirationEntry<TKey, TValue>? entry))
            return false;

        using (entry.Lock.LockSync())
        {
            ThrowIfDisposed();

            if (!IsCurrentEntry(key, entry))
                return false;

            if (!entry.HasValue)
                return false;

            if (IsExpired(entry))
            {
                ExpireEntryNoLockSync(key, entry);
                return false;
            }

            value = entry.Value;
            ResetExpirationNoLock(entry);
            return true;
        }
    }

    public TValue GetSync(TKey key, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            ThrowIfDisposed();

            SlidingExpirationEntry<TKey, TValue> entry = GetOrAddEntry(key);

            using (entry.Lock.LockSync(cancellationToken))
            {
                ThrowIfDisposed();

                if (!IsCurrentEntry(key, entry))
                    continue;

                ExpireValueForReuseNoLockSync(entry);

                try
                {
                    TValue value = GetOrCreateNoLock(entry, key, cancellationToken).AwaitSync();
                    ResetExpirationNoLock(entry);
                    return value;
                }
                catch
                {
                    RemoveEmptyEntryNoLock(key, entry);
                    throw;
                }
            }
        }
    }

    public ValueTask<TValue> Get<TState>(TState state, Func<TState, TKey> keyFactory, CancellationToken cancellationToken = default) where TState : notnull
    {
        TKey key = keyFactory(state);
        return Get(key, cancellationToken);
    }

    public TValue GetSync<TState>(TState state, Func<TState, TKey> keyFactory, CancellationToken cancellationToken = default) where TState : notnull
    {
        TKey key = keyFactory(state);
        return GetSync(key, cancellationToken);
    }

    public SlidingExpirationSingletonKeyDictionary<TKey, TValue> Initialize<TState>(TState state,
        Func<TState, TKey, CancellationToken, ValueTask<TValue>> factory) where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(factory);

        SetFactory((key, cancellationToken) => factory(state, key, cancellationToken));
        return this;
    }

    public void SetInitialization(Func<TKey, ValueTask<TValue>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        SetFactory((key, _) => func(key));
    }

    public void SetInitialization(Func<TKey, CancellationToken, ValueTask<TValue>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        SetFactory(func);
    }

    public void SetInitialization(Func<ValueTask<TValue>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        SetFactory((_, _) => func());
    }

    public void SetInitialization(Func<TValue> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        SetFactory((_, _) => new ValueTask<TValue>(func()));
    }

    public void SetInitialization(Func<TKey, TValue> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        SetFactory((key, _) => new ValueTask<TValue>(func(key)));
    }

    public void SetInitialization(Func<TKey, CancellationToken, TValue> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        SetFactory((key, cancellationToken) => new ValueTask<TValue>(func(key, cancellationToken)));
    }

    public bool TryRemove(TKey key, out TValue? value)
    {
        value = default;
        ThrowIfDisposed();

        if (!_entries.TryGetValue(key, out SlidingExpirationEntry<TKey, TValue>? entry))
            return false;

        using (entry.Lock.LockSync())
        {
            ThrowIfDisposed();

            if (!TryRemoveEntry(key, entry))
                return false;

            entry.DisposeTimer();
            return entry.TryTakeValue(out value);
        }
    }

    public async ValueTask<bool> TryRemoveAndDispose(TKey key)
    {
        ThrowIfDisposed();

        if (!_entries.TryGetValue(key, out SlidingExpirationEntry<TKey, TValue>? entry))
            return false;

        using (await entry.Lock.Lock(CancellationToken.None).NoSync())
        {
            ThrowIfDisposed();

            if (!TryRemoveEntry(key, entry))
                return false;

            await entry.DisposeTimerAsync().NoSync();

            if (!entry.TryTakeValue(out TValue? value))
                return false;

            await DisposeValue(value).NoSync();
            return true;
        }
    }

    public bool TryRemoveAndDisposeSync(TKey key)
    {
        ThrowIfDisposed();

        if (!_entries.TryGetValue(key, out SlidingExpirationEntry<TKey, TValue>? entry))
            return false;

        using (entry.Lock.LockSync())
        {
            ThrowIfDisposed();

            if (!TryRemoveEntry(key, entry))
                return false;

            entry.DisposeTimer();

            if (!entry.TryTakeValue(out TValue? value))
                return false;

            DisposeValueSync(value);
            return true;
        }
    }

    public ValueTask<bool> Remove(TKey key, CancellationToken cancellationToken = default) => TryRemoveAndDispose(key);

    public bool RemoveSync(TKey key, CancellationToken cancellationToken = default) => TryRemoveAndDisposeSync(key);

    public ValueTask<bool> Evict(TKey key, CancellationToken cancellationToken = default) => TryRemoveAndDispose(key);

    public bool EvictSync(TKey key, CancellationToken cancellationToken = default) => TryRemoveAndDisposeSync(key);

    public Dictionary<TKey, TValue> GetAllSync()
    {
        ThrowIfDisposed();

        var result = new Dictionary<TKey, TValue>();

        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            using (kvp.Value.Lock.LockSync())
            {
                if (TryGetSnapshotValueNoLock(kvp.Key, kvp.Value, out TValue? value))
                    result[kvp.Key] = value!;
            }
        }

        return result;
    }

    public async ValueTask<Dictionary<TKey, TValue>> GetAll(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var result = new Dictionary<TKey, TValue>();

        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            using (await kvp.Value.Lock.Lock(cancellationToken).NoSync())
            {
                (bool success, TValue? value) = await TryGetSnapshotValueNoLock(kvp.Key, kvp.Value).NoSync();

                if (success)
                    result[kvp.Key] = value!;
            }
        }

        return result;
    }

    public List<TKey> GetKeysSync()
    {
        ThrowIfDisposed();

        var result = new List<TKey>();

        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            using (kvp.Value.Lock.LockSync())
            {
                if (TryGetSnapshotValueNoLock(kvp.Key, kvp.Value, out _))
                    result.Add(kvp.Key);
            }
        }

        return result;
    }

    public async ValueTask<List<TKey>> GetKeys(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var result = new List<TKey>();

        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            using (await kvp.Value.Lock.Lock(cancellationToken).NoSync())
            {
                (bool success, _) = await TryGetSnapshotValueNoLock(kvp.Key, kvp.Value).NoSync();

                if (success)
                    result.Add(kvp.Key);
            }
        }

        return result;
    }

    public List<TValue> GetValuesSync()
    {
        ThrowIfDisposed();

        var result = new List<TValue>();

        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            using (kvp.Value.Lock.LockSync())
            {
                if (TryGetSnapshotValueNoLock(kvp.Key, kvp.Value, out TValue? value))
                    result.Add(value!);
            }
        }

        return result;
    }

    public async ValueTask<List<TValue>> GetValues(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var result = new List<TValue>();

        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            using (await kvp.Value.Lock.Lock(cancellationToken).NoSync())
            {
                (bool success, TValue? value) = await TryGetSnapshotValueNoLock(kvp.Key, kvp.Value).NoSync();

                if (success)
                    result.Add(value!);
            }
        }

        return result;
    }

    public void ClearSync()
    {
        ThrowIfDisposed();
        ClearEntriesSync();
    }

    public async ValueTask Clear(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await ClearEntries(cancellationToken).NoSync();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        ClearEntriesSync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await ClearEntries(CancellationToken.None).NoSync();
    }

    internal void QueueExpiration(SlidingExpirationEntry<TKey, TValue> entry)
    {
        if (Volatile.Read(ref _disposed) == 1)
            return;

        _ = ExpireFromTimer(entry);
    }

    private async ValueTask<TValue> GetOrCreateNoLock(SlidingExpirationEntry<TKey, TValue> entry, TKey key, CancellationToken cancellationToken)
    {
        if (entry.HasValue)
            return entry.Value!;

        Func<TKey, CancellationToken, ValueTask<TValue>>? factory = _factory;

        if (factory is null)
            throw new InvalidOperationException("Initialization func for SlidingExpirationSingletonKeyDictionary cannot be null");

        TValue value = await factory(key, cancellationToken).NoSync();

        entry.Value = value;
        entry.HasValue = true;

        return value;
    }

    private void SetFactory(Func<TKey, CancellationToken, ValueTask<TValue>> factory)
    {
        ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _factory, factory, null) is not null)
            throw new InvalidOperationException("Setting the initialization of a SlidingExpirationSingletonKeyDictionary after it has already been set is not allowed");
    }

    private SlidingExpirationEntry<TKey, TValue> GetOrAddEntry(TKey key)
    {
        return _entries.GetOrAdd(key, static (k, owner) => new SlidingExpirationEntry<TKey, TValue>(owner, k), this);
    }

    private void ResetExpirationNoLock(SlidingExpirationEntry<TKey, TValue> entry)
    {
        entry.ExpiresAtTick = Environment.TickCount64 + _slidingExpirationMilliseconds;
        entry.Timer.Change(SlidingExpiration, Timeout.InfiniteTimeSpan);
    }

    private async ValueTask ExpireValueForReuseNoLock(SlidingExpirationEntry<TKey, TValue> entry)
    {
        if (!entry.HasValue || !IsExpired(entry))
            return;

        if (entry.TryTakeValue(out TValue? value))
            await DisposeValue(value).NoSync();
    }

    private void ExpireValueForReuseNoLockSync(SlidingExpirationEntry<TKey, TValue> entry)
    {
        if (!entry.HasValue || !IsExpired(entry))
            return;

        if (entry.TryTakeValue(out TValue? value))
            DisposeValueSync(value);
    }

    private bool TryGetSnapshotValueNoLock(TKey key, SlidingExpirationEntry<TKey, TValue> entry, out TValue? value)
    {
        value = default;

        if (!IsCurrentEntry(key, entry) || !entry.HasValue)
            return false;

        if (IsExpired(entry))
        {
            ExpireEntryNoLockSync(key, entry);
            return false;
        }

        value = entry.Value;
        return true;
    }

    private async ValueTask<(bool Success, TValue? Value)> TryGetSnapshotValueNoLock(TKey key, SlidingExpirationEntry<TKey, TValue> entry)
    {
        if (!IsCurrentEntry(key, entry) || !entry.HasValue)
            return (false, default);

        if (IsExpired(entry))
        {
            await ExpireEntryNoLock(key, entry).NoSync();
            return (false, default);
        }

        return (true, entry.Value);
    }

    private void RemoveEmptyEntryNoLock(TKey key, SlidingExpirationEntry<TKey, TValue> entry)
    {
        if (entry.HasValue)
            return;

        if (TryRemoveEntry(key, entry))
            entry.DisposeTimer();
    }

    private async ValueTask ExpireEntryNoLock(TKey key, SlidingExpirationEntry<TKey, TValue> entry)
    {
        if (!TryRemoveEntry(key, entry))
            return;

        await entry.DisposeTimerAsync().NoSync();

        if (entry.TryTakeValue(out TValue? value))
            await DisposeValue(value).NoSync();
    }

    private void ExpireEntryNoLockSync(TKey key, SlidingExpirationEntry<TKey, TValue> entry)
    {
        if (!TryRemoveEntry(key, entry))
            return;

        entry.DisposeTimer();

        if (entry.TryTakeValue(out TValue? value))
            DisposeValueSync(value);
    }

    private async ValueTask ClearEntries(CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            SlidingExpirationEntry<TKey, TValue> entry = kvp.Value;

            if (!TryRemoveEntry(kvp.Key, entry))
                continue;

            using (await entry.Lock.Lock(cancellationToken).NoSync())
            {
                await entry.DisposeTimerAsync().NoSync();

                if (entry.TryTakeValue(out TValue? value))
                    await DisposeValue(value).NoSync();
            }
        }
    }

    private void ClearEntriesSync()
    {
        foreach (KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>> kvp in _entries)
        {
            SlidingExpirationEntry<TKey, TValue> entry = kvp.Value;

            if (!TryRemoveEntry(kvp.Key, entry))
                continue;

            using (entry.Lock.LockSync())
            {
                entry.DisposeTimer();

                if (entry.TryTakeValue(out TValue? value))
                    DisposeValueSync(value);
            }
        }
    }

    private async ValueTask ExpireFromTimer(SlidingExpirationEntry<TKey, TValue> entry)
    {
        try
        {
            await Expire(entry).NoSync();
        }
        catch
        {
            // Timer callbacks cannot surface failures to callers.
        }
    }

    private async ValueTask Expire(SlidingExpirationEntry<TKey, TValue> entry)
    {
        using (await entry.Lock.Lock(CancellationToken.None).NoSync())
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;

            if (!IsCurrentEntry(entry.Key, entry))
                return;

            if (!entry.HasValue)
            {
                if (TryRemoveEntry(entry.Key, entry))
                    await entry.DisposeTimerAsync().NoSync();

                return;
            }

            long remainingMilliseconds = entry.ExpiresAtTick - Environment.TickCount64;

            if (remainingMilliseconds > 0)
            {
                entry.Timer.Change(TimeSpan.FromMilliseconds(remainingMilliseconds), Timeout.InfiniteTimeSpan);
                return;
            }

            await ExpireEntryNoLock(entry.Key, entry).NoSync();
        }
    }

    private bool IsCurrentEntry(TKey key, SlidingExpirationEntry<TKey, TValue> entry)
    {
        return _entries.TryGetValue(key, out SlidingExpirationEntry<TKey, TValue>? current) && ReferenceEquals(current, entry);
    }

    private bool TryRemoveEntry(TKey key, SlidingExpirationEntry<TKey, TValue> entry)
    {
        return ((ICollection<KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>>>) _entries)
            .Remove(new KeyValuePair<TKey, SlidingExpirationEntry<TKey, TValue>>(key, entry));
    }

    private static bool IsExpired(SlidingExpirationEntry<TKey, TValue> entry)
    {
        return entry.ExpiresAtTick <= Environment.TickCount64;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 1)
            throw new ObjectDisposedException(nameof(SlidingExpirationSingletonKeyDictionary<TKey, TValue>));
    }

    private static void ValidateSlidingExpiration(TimeSpan slidingExpiration)
    {
        if (slidingExpiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(slidingExpiration), "Sliding expiration must be greater than zero.");
    }

    private static void DisposeValueSync(TValue? value)
    {
        switch (value)
        {
            case IAsyncDisposable asyncDisposable:
                asyncDisposable.DisposeAsync().AwaitSync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private static async ValueTask DisposeValue(TValue? value)
    {
        switch (value)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().NoSync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
