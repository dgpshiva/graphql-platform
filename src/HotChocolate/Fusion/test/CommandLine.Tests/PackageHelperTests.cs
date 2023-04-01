using System.Collections.Concurrent;
using System.CommandLine.Parsing;
using CookieCrumble;
using HotChocolate.Fusion;
using HotChocolate.Fusion.CommandLine;
using HotChocolate.Fusion.CommandLine.Helpers;
using HotChocolate.Fusion.Composition;
using HotChocolate.Fusion.Shared;
using HotChocolate.Skimmed.Serialization;
using static HotChocolate.Fusion.CommandLine.Helpers.PackageHelper;
using static HotChocolate.Fusion.Shared.DemoProjectSchemaExtensions;

namespace CommandLine.Tests;

public class PackageHelperTests : IDisposable
{
    private readonly ConcurrentBag<string> _files = new();

    [Fact]
    public async Task Create_Subgraph_Package()
    {
        // arrange
        using var demoProject = await DemoProject.CreateAsync();
        var accountConfig = demoProject.Accounts.ToConfiguration(AccountsExtensionSdl);
        var account = CreateFiles(accountConfig);
        var packageFile = CreateTempFile();

        // act
        await CreateSubgraphPackageAsync(
            packageFile,
            new SubgraphFiles(
                account.SchemaFile,
                account.TransportConfigFile,
                account.ExtensionFiles));

        // assert
        Assert.True(File.Exists(packageFile));
        var accountConfigRead = await ReadSubgraphPackageAsync(packageFile);
        accountConfig.MatchSnapshot();
        accountConfigRead.MatchSnapshot();
    }

    private Files CreateFiles(SubgraphConfiguration configuration)
    {
        var files = new Files(CreateTempFile(), CreateTempFile(), new[] { CreateTempFile() });
        var configJson = FormatSubgraphConfig(new(configuration.Name, configuration.Clients));
        File.WriteAllText(files.SchemaFile, configuration.Schema);
        File.WriteAllText(files.TransportConfigFile, configJson);
        File.WriteAllText(files.ExtensionFiles[0], configuration.Extensions[0]);
        return files;
    }

    private string CreateTempFile()
    {
        var file = Path.GetTempFileName();
        _files.Add(file);
        return file;
    }

    public void Dispose()
    {
        while (_files.TryTake(out var file))
        {
            File.Delete(file);
        }
    }

    public record Files(string SchemaFile, string TransportConfigFile, string[] ExtensionFiles);
}

public class ComposeHelperTests : IDisposable
{
    private readonly ConcurrentBag<string> _files = new();

    [Fact]
    public async Task Compose_Fusion_Graph()
    {
        // arrange
        using var demoProject = await DemoProject.CreateAsync();
        var accountConfig = demoProject.Accounts.ToConfiguration(AccountsExtensionSdl);
        var account = CreateFiles(accountConfig);
        var subgraphPackageFile = CreateTempFile();

        await CreateSubgraphPackageAsync(
            subgraphPackageFile,
            new SubgraphFiles(
                account.SchemaFile,
                account.TransportConfigFile,
                account.ExtensionFiles));

        var packageFile = CreateTempFile();

        // act
        var app = App.CreateBuilder().Build();
        await app.InvokeAsync(new[]
        {
            "compose",
            "-p",
            packageFile,
            "-s",
            subgraphPackageFile
        });

        // assert
        Assert.True(File.Exists(packageFile));

        await using var package = FusionGraphPackage.Open(packageFile, FileAccess.Read);

        var fusionGraph = await package.GetFusionGraphAsync();
        var schema = await package.GetSchemaAsync();
        var subgraphs = await package.GetSubgraphConfigurationsAsync();

        var snapshot = new Snapshot();

        snapshot.Add(schema, "Schema Document");
        snapshot.Add(fusionGraph, "Fusion Graph Document");

        foreach (var subgraph in subgraphs)
        {
            snapshot.Add(subgraph, $"{subgraph.Name} Subgraph Configuration");
        }

        snapshot.MatchSnapshot();
    }

    [Fact]
    public async Task Compose_Fusion_Graph_Append_Subgraph()
    {
        // arrange
        using var demoProject = await DemoProject.CreateAsync();
        var accountConfig = demoProject.Accounts.ToConfiguration(AccountsExtensionSdl);
        var account = CreateFiles(accountConfig);
        var accountSubgraphPackageFile = CreateTempFile();

        await CreateSubgraphPackageAsync(
            accountSubgraphPackageFile,
            new SubgraphFiles(
                account.SchemaFile,
                account.TransportConfigFile,
                account.ExtensionFiles));

        var reviewConfig = demoProject.Reviews2.ToConfiguration(ReviewsExtensionSdl);
        var review = CreateFiles(reviewConfig);
        var reviewSubgraphPackageFile = CreateTempFile();

        await CreateSubgraphPackageAsync(
            reviewSubgraphPackageFile,
            new SubgraphFiles(
                review.SchemaFile,
                review.TransportConfigFile,
                review.ExtensionFiles));

        var packageFile = CreateTempFile();

        var app = App.CreateBuilder().Build();
        await app.InvokeAsync(new[]
        {
            "compose",
            "-p",
            packageFile,
            "-s",
            accountSubgraphPackageFile
        });

        // act
        app = App.CreateBuilder().Build();
        await app.InvokeAsync(new[]
        {
            "compose",
            "-p",
            packageFile,
            "-s",
            reviewSubgraphPackageFile
        });

        // assert
        Assert.True(File.Exists(packageFile));

        await using var package = FusionGraphPackage.Open(packageFile, FileAccess.Read);

        var fusionGraph = await package.GetFusionGraphAsync();
        var schema = await package.GetSchemaAsync();
        var subgraphs = await package.GetSubgraphConfigurationsAsync();

        var snapshot = new Snapshot();

        snapshot.Add(schema, "Schema Document");
        snapshot.Add(fusionGraph, "Fusion Graph Document");

        foreach (var subgraph in subgraphs)
        {
            snapshot.Add(subgraph, $"{subgraph.Name} Subgraph Configuration");
        }

        snapshot.MatchSnapshot();
    }

    private Files CreateFiles(SubgraphConfiguration configuration)
    {
        var files = new Files(CreateTempFile(), CreateTempFile(), new[] { CreateTempFile() });
        var configJson = FormatSubgraphConfig(new(configuration.Name, configuration.Clients));
        File.WriteAllText(files.SchemaFile, configuration.Schema);
        File.WriteAllText(files.TransportConfigFile, configJson);
        File.WriteAllText(files.ExtensionFiles[0], configuration.Extensions[0]);
        return files;
    }

    private string CreateTempFile()
    {
        var file = Path.GetTempFileName();
        _files.Add(file);
        return file;
    }

    public void Dispose()
    {
        while (_files.TryTake(out var file))
        {
            File.Delete(file);
        }
    }

    public record Files(string SchemaFile, string TransportConfigFile, string[] ExtensionFiles);
}
