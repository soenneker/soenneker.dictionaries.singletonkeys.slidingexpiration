using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Dictionaries.SingletonKeys.SlidingExpiration.Abstract;

/// <summary>
/// A keyed singleton cache that disposes values after they have not been retrieved for the configured sliding expiration.
/// </summary>
/// <typeparam name="TKey">The key type. Must be non-null.</typeparam>
/// <typeparam name="TValue">The cached value type.</typeparam>
public interface ISlidingExpirationSingletonKeyDictionary<TKey, TValue> : IDisposable, IAsyncDisposable where TKey : notnull
{
    /// <summary>
    /// Gets the idle duration after which a cached value is evicted when it has not been retrieved.
    /// </summary>
    TimeSpan SlidingExpiration { get; }

    /// <summary>
    /// Retrieves the singleton value associated with <paramref name="key"/>, creating and caching it if it does not already exist.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    [Pure]
    ValueTask<TValue> Get(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a cached value for <paramref name="key"/> without initializing it if missing.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    [Pure]
    bool TryGet(TKey key, out TValue? value);

    /// <summary>
    /// Synchronously retrieves the singleton value associated with <paramref name="key"/>, creating and caching it if it does not already exist.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    [Pure]
    TValue GetSync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the singleton value associated with a key derived from <paramref name="state"/>.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    [Pure]
    ValueTask<TValue> Get<TState>(TState state, Func<TState, TKey> keyFactory, CancellationToken cancellationToken = default) where TState : notnull;

    /// <summary>
    /// Synchronously retrieves the singleton value associated with a key derived from <paramref name="state"/>.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    [Pure]
    TValue GetSync<TState>(TState state, Func<TState, TKey> keyFactory, CancellationToken cancellationToken = default) where TState : notnull;

    /// <summary>
    /// Configures the stateful initialization function used to create values for missing keys.
    /// </summary>
    SlidingExpirationSingletonKeyDictionary<TKey, TValue> Initialize<TState>(TState state, Func<TState, TKey, CancellationToken, ValueTask<TValue>> factory)
        where TState : notnull;

    /// <summary>
    /// Sets the async initialization function used to create values for a key.
    /// </summary>
    void SetInitialization(Func<TKey, ValueTask<TValue>> func);

    /// <summary>
    /// Sets the async initialization function used to create values for a key, with cancellation support.
    /// </summary>
    void SetInitialization(Func<TKey, CancellationToken, ValueTask<TValue>> func);

    /// <summary>
    /// Sets the async initialization function used to create values without a key.
    /// </summary>
    void SetInitialization(Func<ValueTask<TValue>> func);

    /// <summary>
    /// Sets the synchronous initialization function used to create values without a key.
    /// </summary>
    void SetInitialization(Func<TValue> func);

    /// <summary>
    /// Sets the synchronous initialization function used to create values for a key.
    /// </summary>
    void SetInitialization(Func<TKey, TValue> func);

    /// <summary>
    /// Sets the synchronous initialization function used to create values for a key, with cancellation support.
    /// </summary>
    void SetInitialization(Func<TKey, CancellationToken, TValue> func);

    /// <summary>
    /// Removes the cached value without disposing it and cancels its sliding expiration.
    /// </summary>
    bool TryRemove(TKey key, out TValue? value);

    /// <summary>
    /// Removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    ValueTask<bool> TryRemoveAndDispose(TKey key);

    /// <summary>
    /// Synchronously removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    bool TryRemoveAndDisposeSync(TKey key);

    /// <summary>
    /// Removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    ValueTask<bool> Remove(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    bool RemoveSync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Strongly evicts the cached value if present and cancels its sliding expiration.
    /// </summary>
    ValueTask<bool> Evict(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously strongly evicts the cached value if present and cancels its sliding expiration.
    /// </summary>
    bool EvictSync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a snapshot of all cached key/value pairs without resetting sliding expirations.
    /// </summary>
    [Pure]
    Dictionary<TKey, TValue> GetAllSync();

    /// <summary>
    /// Retrieves a snapshot of all cached key/value pairs without resetting sliding expirations.
    /// </summary>
    [Pure]
    ValueTask<Dictionary<TKey, TValue>> GetAll(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a snapshot of all cached keys without resetting sliding expirations.
    /// </summary>
    [Pure]
    List<TKey> GetKeysSync();

    /// <summary>
    /// Retrieves a snapshot of all cached keys without resetting sliding expirations.
    /// </summary>
    [Pure]
    ValueTask<List<TKey>> GetKeys(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a snapshot of all cached values without resetting sliding expirations.
    /// </summary>
    [Pure]
    List<TValue> GetValuesSync();

    /// <summary>
    /// Retrieves a snapshot of all cached values without resetting sliding expirations.
    /// </summary>
    [Pure]
    ValueTask<List<TValue>> GetValues(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears and disposes all cached values and cancels all sliding expirations.
    /// </summary>
    void ClearSync();

    /// <summary>
    /// Clears and disposes all cached values and cancels all sliding expirations.
    /// </summary>
    ValueTask Clear(CancellationToken cancellationToken = default);
}
