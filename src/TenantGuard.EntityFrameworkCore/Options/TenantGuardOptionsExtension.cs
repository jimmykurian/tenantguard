// <copyright file="TenantGuardOptionsExtension.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace TenantGuard;

internal sealed class TenantGuardOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public TenantGuardOptionsExtension(TenantGuardOptions options)
    {
        Options = options;
    }

    public TenantGuardOptions Options { get; }

    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => new EntityFrameworkServicesBuilder(services).TryAdd<IConventionSetPlugin, TenantGuardConventionSetPlugin>();

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        private string? _logFragment;

        public ExtensionInfo(TenantGuardOptionsExtension extension)
            : base(extension)
        {
        }

        public override bool IsDatabaseProvider => false;

        public override string LogFragment
            => _logFragment ??= $"TenantGuard Enforcement={Options.Enforcement} ";

        private TenantGuardOptions Options => ((TenantGuardOptionsExtension)Extension).Options;

        public override int GetServiceProviderHashCode()
            => Options.GetEquivalenceHashCode();

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo info && info.Options.IsEquivalentTo(Options);

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["TenantGuard:Options"] = Options.GetEquivalenceHashCode().ToString(CultureInfo.InvariantCulture);
    }
}
