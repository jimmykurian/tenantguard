// <copyright file="TenantGuardEvents.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;

// Keeps the names of the events TenantGuard logs, for the smoke run.
internal sealed class TenantGuardEvents : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> _names = new();

    public bool Contains(string eventName) => _names.Contains(eventName);

    public ILogger CreateLogger(string categoryName) => categoryName == "TenantGuard" ? this : NullLogger.Instance;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _names.Enqueue(eventId.Name ?? string.Empty);

    public void Dispose()
    {
    }
}
