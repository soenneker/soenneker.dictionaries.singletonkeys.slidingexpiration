[![](https://img.shields.io/nuget/v/soenneker.dictionaries.singletonkeys.slidingexpiration.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.singletonkeys.slidingexpiration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.dictionaries.singletonkeys.slidingexpiration/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.dictionaries.singletonkeys.slidingexpiration/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.dictionaries.singletonkeys.slidingexpiration.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.dictionaries.singletonkeys.slidingexpiration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.dictionaries.singletonkeys.slidingexpiration/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.dictionaries.singletonkeys.slidingexpiration/actions/workflows/codeql.yml)

# Soenneker.Dictionaries.SingletonKeys.SlidingExpiration

A keyed singleton cache that refreshes a per-entry idle timer on retrieval and disposes values after the timer expires.

## Installation

```bash
dotnet add package Soenneker.Dictionaries.SingletonKeys.SlidingExpiration
```

## Usage

```csharp
using Soenneker.Dictionaries.SingletonKeys.SlidingExpiration;

await using var results = new SlidingExpirationSingletonKeyDictionary<string, LookupResult>(
    slidingExpiration: TimeSpan.FromMinutes(5),
    func: async (key, cancellationToken) =>
        await LoadResult(key, cancellationToken));

LookupResult result = await results.Get("customer:42", cancellationToken);
```

Concurrent requests for one missing key share one factory execution. Different keys initialize concurrently. Factory failures are not cached, so a later request can retry.

`Get`, `GetSync`, and a successful `TryGet` restart the key’s expiration from the time of that retrieval. Snapshot methods do not refresh expiration. When a timer fires, the value is removed and disposed; a later `Get` creates a replacement.

The expiration must be positive and no greater than `4,294,967,294` milliseconds, the supported `System.Threading.Timer` range.

## Important lifetime limitation

This API returns the cached value directly. It cannot know how long the caller continues using that reference, so the timer may dispose a value while application code still holds it.

Use this package for immutable/non-disposable values, or only where callers finish well inside the idle period and external coordination prevents expiry. For database connections, clients, streams, or other owned disposable resources, use `Soenneker.Dictionaries.SingletonKeys.LeasedExpiration`; its lease keeps the value alive until the caller releases it.

## Removal and snapshots

```csharp
bool removed = await results.Remove("customer:42", cancellationToken);

Dictionary<string, LookupResult> snapshot = await results.GetAll(cancellationToken);
```

`Remove`, `TryRemoveAndDispose`, and `Evict` remove and dispose the value. `TryRemove(key, out value)` cancels expiration but transfers value ownership to the caller without disposing it.

`GetAll`, `GetKeys`, and `GetValues` return new collections and do not refresh idle timers. They inspect entries one at a time rather than freezing all mutations globally, so treat them as observational snapshots under concurrent activity.

`Clear` removes all current entries, cancels their timers, and disposes their values. Dictionary disposal is terminal. Both operations can invalidate references already returned to callers, which is another reason to use the leased variant when value lifetime matters.

Each cached key owns a timer. Consider a centralized/bucketed expiration design for very high key cardinality.
