// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Winsdk.Cli.Helpers;

namespace Winsdk.Cli.Tests;

public class BaseCommandTests
{
    private ServiceProvider _serviceProvider = null!;
    protected StringWriter ConsoleStdOut { private set; get; } = null!;
    protected StringWriter ConsoleStdErr { private set; get; } = null!;

    [TestInitialize]
    public void SetupBase()
    {
        ConsoleStdOut = new StringWriter();
        ConsoleStdErr = new StringWriter();

        var services = new ServiceCollection()
            .ConfigureServices()
            .ConfigureCommands()
            .AddLogging(b =>
            {
                b.ClearProviders();
                b.AddTextWriterLogger(ConsoleStdOut, ConsoleStdErr);
                b.SetMinimumLevel(LogLevel.Debug);
            });

        _serviceProvider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void CleanupBase()
    {
        _serviceProvider?.Dispose();
        ConsoleStdOut?.Dispose();
        ConsoleStdErr?.Dispose();
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}
