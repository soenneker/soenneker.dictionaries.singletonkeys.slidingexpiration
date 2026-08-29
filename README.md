[![](https://img.shields.io/nuget/v/soenneker.dictionaries.singletonkeys.slidingexpiration.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.singletonkeys.slidingexpiration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.dictionaries.singletonkeys.slidingexpiration/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.dictionaries.singletonkeys.slidingexpiration/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.dictionaries.singletonkeys.slidingexpiration.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.singletonkeys.slidingexpiration/)

# Soenneker.Dictionaries.SingletonKeys.SlidingExpiration

A keyed singleton cache that disposes values after they have not been retrieved for the configured sliding expiration.

## Install

```bash
dotnet add package Soenneker.Dictionaries.SingletonKeys.SlidingExpiration
```

## Quick start

```csharp
using Soenneker.Dictionaries.SingletonKeys.SlidingExpiration.Abstract;

ISlidingExpirationSingletonKeyDictionary<TKey, TValue> slidingExpirationSingletonKeyDictionary = /* resolve from DI */;
var result = await slidingExpirationSingletonKeyDictionary.Get(/* supply key */ default!, default);
```

Retrieves the singleton value associated with `key`, creating and caching it if it does not already exist. Successful retrieval resets that key's sliding expiration.

## What you get

- `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>` — A keyed singleton cache that disposes values after they have not been retrieved for the configured sliding expiration.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.SlidingExpiration` | Gets the idle duration after which a cached value is evicted when it has not been retrieved. | Gets the idle duration after which a cached value is evicted when it has not been retrieved. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.Get(key, cancellationToken)` | Retrieves the singleton value associated with `key`, creating and caching it if it does not already exist. Successful retrieval resets that key's sliding expiration. | A task whose result is the value returned by get. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.TryGet(key, value)` | Attempts to retrieve a cached value for `key` without initializing it if missing. Successful retrieval resets that key's sliding expiration. | true if the requested update was applied; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.GetSync(key, cancellationToken)` | Synchronously retrieves the singleton value associated with `key`, creating and caching it if it does not already exist. Successful retrieval resets that key's sliding expiration. | The resulting value. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.Get(state, keyFactory, cancellationToken)` | Retrieves the singleton value associated with a key derived from `state`. Successful retrieval resets that key's sliding expiration. | A task whose result is the value returned by get. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.GetSync(state, keyFactory, cancellationToken)` | Synchronously retrieves the singleton value associated with a key derived from `state`. Successful retrieval resets that key's sliding expiration. | The resulting value. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.Initialize(state, factory)` | Configures the stateful initialization function used to create values for missing keys. | The resulting sliding Expiration Singleton Key Dictionary. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.SetInitialization(func)` | Sets the async initialization function used to create values for a key. | Returns no value; the requested change is complete when the method returns. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.TryRemove(key, value)` | Removes the cached value without disposing it and cancels its sliding expiration. | true if removes the cached value without disposing it and cancels its sliding expiration; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.TryRemoveAndDispose(key)` | Removes and disposes the cached value if present and cancels its sliding expiration. | true if removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.TryRemoveAndDisposeSync(key)` | Synchronously removes and disposes the cached value if present and cancels its sliding expiration. | true if synchronously removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.Remove(key, cancellationToken)` | Removes and disposes the cached value if present and cancels its sliding expiration. | true if removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.RemoveSync(key, cancellationToken)` | Synchronously removes and disposes the cached value if present and cancels its sliding expiration. | true if synchronously removes and disposes the cached value if present and cancels its sliding expiration; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.Evict(key, cancellationToken)` | Strongly evicts the cached value if present and cancels its sliding expiration. | true if strongly evicts the cached value if present and cancels its sliding expiration; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.EvictSync(key, cancellationToken)` | Synchronously strongly evicts the cached value if present and cancels its sliding expiration. | true if synchronously strongly evicts the cached value if present and cancels its sliding expiration; otherwise, false. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.GetAllSync()` | Retrieves a snapshot of all cached key/value pairs without resetting sliding expirations. | The requested dictionary. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.GetAll(cancellationToken)` | Retrieves a snapshot of all cached key/value pairs without resetting sliding expirations. | A task whose result is the requested dictionary. |
| `ISlidingExpirationSingletonKeyDictionary<TKey, TValue>.GetKeysSync()` | Retrieves a snapshot of all cached keys without resetting sliding expirations. | The requested collection. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
- Calls that return a cached or singleton value reuse the same instance until the owning service is disposed.
- Dispose instances you own when their scope ends so held resources can be released.
