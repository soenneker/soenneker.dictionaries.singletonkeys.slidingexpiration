using System;
using System.Collections.Generic;
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
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the value returned by get.</returns>
    ValueTask<TValue> Get(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a cached value for <paramref name="key"/> without initializing it if missing.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="value">Receives the matching value when the lookup succeeds.</param>
    /// <returns>true if the requested update was applied; otherwise, false.</returns>
    bool TryGet(TKey key, out TValue? value);

    /// <summary>
    /// Synchronously retrieves the singleton value associated with <paramref name="key"/>, creating and caching it if it does not already exist.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The resulting value.</returns>
    TValue GetSync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the singleton value associated with a key derived from <paramref name="state"/>.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    /// <typeparam name="TState">Type of state passed to the callback.</typeparam>
    /// <param name="state">State value used by the variant.</param>
    /// <param name="keyFactory">Function that derives a key from the supplied state.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the value returned by get.</returns>
    ValueTask<TValue> Get<TState>(TState state, Func<TState, TKey> keyFactory, CancellationToken cancellationToken = default) where TState : notnull;

    /// <summary>
    /// Synchronously retrieves the singleton value associated with a key derived from <paramref name="state"/>.
    /// Successful retrieval resets that key's sliding expiration.
    /// </summary>
    /// <typeparam name="TState">Type of state passed to the callback.</typeparam>
    /// <param name="state">State value used by the variant.</param>
    /// <param name="keyFactory">Function that derives a key from the supplied state.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The resulting value.</returns>
    TValue GetSync<TState>(TState state, Func<TState, TKey> keyFactory, CancellationToken cancellationToken = default) where TState : notnull;

    /// <summary>
    /// Configures the stateful initialization function used to create values for missing keys.
    /// </summary>
    /// <typeparam name="TState">Type of state passed to the callback.</typeparam>
    /// <param name="state">State value used by the variant.</param>
    /// <param name="factory">Factory used to create a value when one is needed.</param>
    /// <returns>The resulting sliding Expiration Singleton Key Dictionary.</returns>
    SlidingExpirationSingletonKeyDictionary<TKey, TValue> Initialize<TState>(TState state, Func<TState, TKey, CancellationToken, ValueTask<TValue>> factory)
        where TState : notnull;

    /// <summary>
    /// Sets the async initialization function used to create values for a key.
    /// </summary>
    /// <param name="func">Function to invoke.</param>
    void SetInitialization(Func<TKey, ValueTask<TValue>> func);

    /// <summary>
    /// Sets the async initialization function used to create values for a key, with cancellation support.
    /// </summary>
    /// <param name="func">Function to invoke.</param>
    void SetInitialization(Func<TKey, CancellationToken, ValueTask<TValue>> func);

    /// <summary>
    /// Sets the async initialization function used to create values without a key.
    /// </summary>
    /// <param name="func">Function to invoke.</param>
    void SetInitialization(Func<ValueTask<TValue>> func);

    /// <summary>
    /// Sets the synchronous initialization function used to create values without a key.
    /// </summary>
    /// <param name="func">Function to invoke.</param>
    void SetInitialization(Func<TValue> func);

    /// <summary>
    /// Sets the synchronous initialization function used to create values for a key.
    /// </summary>
    /// <param name="func">Function to invoke.</param>
    void SetInitialization(Func<TKey, TValue> func);

    /// <summary>
    /// Sets the synchronous initialization function used to create values for a key, with cancellation support.
    /// </summary>
    /// <param name="func">Function to invoke.</param>
    void SetInitialization(Func<TKey, CancellationToken, TValue> func);

    /// <summary>
    /// Removes the cached value without disposing it and cancels its sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="value">Value to test, add, or remove from the set.</param>
    /// <returns>true if removes the cached value without disposing it and cancels its sliding expiration; otherwise, false.</returns>
    bool TryRemove(TKey key, out TValue? value);

    /// <summary>
    /// Removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <returns>true if removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false.</returns>
    ValueTask<bool> TryRemoveAndDispose(TKey key);

    /// <summary>
    /// Synchronously removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <returns>true if synchronously removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false.</returns>
    bool TryRemoveAndDisposeSync(TKey key);

    /// <summary>
    /// Removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false.</returns>
    ValueTask<bool> Remove(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously removes and disposes the cached value if present and cancels its sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if synchronously removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false.</returns>
    bool RemoveSync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Strongly evicts the cached value if present and cancels its sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if strongly evicts the cached value if present and cancels its sliding expiration; otherwise, false.</returns>
    ValueTask<bool> Evict(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously strongly evicts the cached value if present and cancels its sliding expiration.
    /// </summary>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if synchronously strongly evicts the cached value if present and cancels its sliding expiration; otherwise, false.</returns>
    bool EvictSync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a snapshot of all cached key/value pairs without resetting sliding expirations.
    /// </summary>
    /// <returns>The requested dictionary.</returns>
    Dictionary<TKey, TValue> GetAllSync();

    /// <summary>
    /// Retrieves a snapshot of all cached key/value pairs without resetting sliding expirations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested dictionary.</returns>
    ValueTask<Dictionary<TKey, TValue>> GetAll(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a snapshot of all cached keys without resetting sliding expirations.
    /// </summary>
    /// <returns>The requested collection.</returns>
    List<TKey> GetKeysSync();

    /// <summary>
    /// Retrieves a snapshot of all cached keys without resetting sliding expirations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Keys.</returns>
    ValueTask<List<TKey>> GetKeys(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a snapshot of all cached values without resetting sliding expirations.
    /// </summary>
    /// <returns>The requested collection.</returns>
    List<TValue> GetValuesSync();

    /// <summary>
    /// Retrieves a snapshot of all cached values without resetting sliding expirations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Values.</returns>
    ValueTask<List<TValue>> GetValues(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears and disposes all cached values and cancels all sliding expirations.
    /// </summary>
    void ClearSync();

    /// <summary>
    /// Clears and disposes all cached values and cancels all sliding expirations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the Sliding Expiration Singleton Key Dictionary has been cleared.</returns>
    ValueTask Clear(CancellationToken cancellationToken = default);
}
