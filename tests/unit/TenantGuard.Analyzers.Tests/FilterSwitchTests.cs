// <copyright file="FilterSwitchTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Verify = TenantGuard.Analyzers.Tests.AnalyzerVerifier<TenantGuard.Analyzers.FilterSwitchAnalyzer>;

namespace TenantGuard.Analyzers.Tests;

// Analyzer TG005 against code that should and shouldn't report it, with ABP's data filter API declared
// as stubs of the same full names.
public sealed class FilterSwitchTests
{
    private const string Abp = """
        namespace Volo.Abp.Data
        {
            public interface IDataFilter
            {
                IDisposable Disable<TFilter>() where TFilter : class;

                IDisposable Enable<TFilter>() where TFilter : class;
            }

            public interface IDataFilter<TFilter> where TFilter : class
            {
                IDisposable Disable();
            }

            public interface ISoftDelete
            {
            }

            public class FilterBase
            {
                public IDisposable Disable<TFilter>() where TFilter : class => null!;

                public IDisposable Enable<TFilter>() where TFilter : class => null!;
            }

            public sealed class InheritedFilter : FilterBase, IDataFilter
            {
            }

            public sealed class DataFilter : IDataFilter
            {
                public IDisposable Disable<TFilter>() where TFilter : class => null!;

                public IDisposable Enable<TFilter>() where TFilter : class => null!;
            }
        }

        namespace Volo.Abp.MultiTenancy
        {
            public interface IMultiTenant
            {
            }
        }

        """;

    [Fact]
    public Task Disabling_ABPs_tenant_filter_is_reported_with_its_name()
    {
        var test = Verify.Create(Abp + """
            public static class Reports
            {
                public static void Run(Volo.Abp.Data.IDataFilter dataFilter)
                {
                    using (dataFilter.{|#0:Disable<Volo.Abp.MultiTenancy.IMultiTenant>()|})
                    {
                    }
                }
            }
            """);
        test.ExpectedDiagnostics.Add(new DiagnosticResult(FilterSwitchAnalyzer.Rule).WithLocation(0).WithMessage(
            "IDataFilter.Disable<IMultiTenant> turns off the tenant filter. Add [AllowCrossTenantAccess(\"why\")] to the method."));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task The_generic_interface_and_an_implementing_class_are_reported()
        => Verify.VerifyAsync(Abp + """
            public static class Reports
            {
                public static void Generic(Volo.Abp.Data.IDataFilter<Volo.Abp.MultiTenancy.IMultiTenant> filter)
                {
                    using (filter.{|TG005:Disable()|})
                    {
                    }
                }

                public static void Concrete(Volo.Abp.Data.DataFilter dataFilter)
                {
                    using (dataFilter.{|TG005:Disable<Volo.Abp.MultiTenancy.IMultiTenant>()|})
                    {
                    }
                }

                public static void Inherited(Volo.Abp.Data.InheritedFilter dataFilter)
                {
                    using (dataFilter.{|TG005:Disable<Volo.Abp.MultiTenancy.IMultiTenant>()|})
                    {
                    }
                }
            }
            """);

    [Fact]
    public Task Other_filters_and_enabling_are_not_reported()
        => Verify.VerifyAsync(Abp + """
            public static class Reports
            {
                public static void Run(Volo.Abp.Data.IDataFilter dataFilter, Volo.Abp.Data.IDataFilter<Volo.Abp.Data.ISoftDelete> softDelete)
                {
                    using (dataFilter.Disable<Volo.Abp.Data.ISoftDelete>())
                    using (dataFilter.Enable<Volo.Abp.MultiTenancy.IMultiTenant>())
                    using (softDelete.Disable())
                    {
                    }
                }
            }
            """);

    [Fact]
    public Task Attribute_clears_it()
        => Verify.VerifyAsync(Abp + """
            public static class Reports
            {
                [AllowCrossTenantAccess("the host's report across tenants")]
                public static void Run(Volo.Abp.Data.IDataFilter dataFilter)
                {
                    using (dataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>())
                    {
                    }
                }
            }
            """);

    [Fact]
    public Task Configured_methods_are_reported_with_their_type_argument_when_it_is_given()
    {
        var test = Verify.Create(
            """
            namespace Acme.Data
            {
                public interface ITenantOwned
                {
                }

                public interface IAudited
                {
                }

                public static class Filters
                {
                    public static void DisableTenant()
                    {
                    }

                    public static void Disable<T>()
                    {
                    }
                }
            }

            public static class Jobs
            {
                public static void Run()
                {
                    Acme.Data.Filters.{|#0:DisableTenant()|};
                    Acme.Data.Filters.{|#1:Disable<Acme.Data.ITenantOwned>()|};
                    Acme.Data.Filters.Disable<Acme.Data.IAudited>();
                }
            }
            """,
            "tenantguard.filter_switch_methods = Acme.Data.Filters.DisableTenant, Acme.Data.Filters.Disable<Acme.Data.ITenantOwned>");
        test.ExpectedDiagnostics.Add(new DiagnosticResult(FilterSwitchAnalyzer.Rule).WithLocation(0).WithArguments("Filters.DisableTenant"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult(FilterSwitchAnalyzer.Rule).WithLocation(1).WithArguments("Filters.Disable<ITenantOwned>"));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Rule_matches_the_spec()
    {
        var rule = FilterSwitchAnalyzer.Rule;

        (rule.Id, rule.DefaultSeverity).Should().Be(("TG005", DiagnosticSeverity.Warning));
        rule.MessageFormat.ToString(CultureInfo.InvariantCulture).Should().Be("{0} turns off the tenant filter. Add [AllowCrossTenantAccess(\"why\")] to the method.");
    }
}
