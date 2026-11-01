// <copyright file="LogCapture.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TenantGuard.EntityFrameworkCore.Tests.Support;

// An ILoggerProvider that keeps every entry, for asserting on TenantGuard's log events.
internal sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<Entry> _entries = new();

    public IReadOnlyList<Entry> TenantGuardEntries
        => [.. _entries.Where(entry => entry.Category == TenantGuardLogging.Category)];

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    internal sealed record Entry(string Category, LogLevel Level, string EventName, string Message);

    private sealed class Logger(LogCapture owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => owner._entries.Enqueue(new Entry(category, logLevel, eventId.Name ?? string.Empty, formatter(state, exception)));
    }
}
