using System;

namespace Soenneker.Dictionaries.SingletonKeys.SlidingExpiration.Tests;

internal sealed class DisposableValue : IDisposable
{
    private readonly Action _onDispose;

    public DisposableValue(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        _onDispose();
    }
}
