// <copyright file="ConsoleLines.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Extensions.Logging;

namespace ValidateTool.Fixture;

// Writes each log line to the console at once, as an app's console logger does, so a test of the tool's
// output sees what the app logs while the tool runs.
public sealed class ConsoleLines : ILoggerProvider, ILogger
{
    public ILogger CreateLogger(string categoryName) => this;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Console.WriteLine($"app log: {formatter(state, exception)}");

    public void Dispose()
    {
    }
}
