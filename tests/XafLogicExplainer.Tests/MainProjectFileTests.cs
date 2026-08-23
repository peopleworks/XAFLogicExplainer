using XafLogicExplainer.Core.Analyzers;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Which project file is read when a module folder holds more than one.
/// </summary>
/// <remarks>
/// A folder with exactly one <c>.csproj</c> is the common case and any rule agrees about it. Real
/// solutions do not stop there — among the applications this was checked against, one module folder
/// holds <c>PWPresupuesto.Module.csproj</c> beside <c>PWPresupuesto.Module.Net10.csproj</c> from a
/// framework migration, and another holds a hand-made <c>"pwLegalOffice - Backup.Module.csproj"</c>.
/// <para>
/// Extraction took whichever entry the file system happened to return first, so the target
/// framework, the package list and the DevExpress version could all come from a backup — and could
/// differ between two machines looking at the same repository, with nothing in the output to say so.
/// </para>
/// </remarks>
public class MainProjectFileTests
{
    private static string ModuleFolder(string folderName, params (string File, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), $"xaflogic-proj-{Guid.NewGuid():N}", folderName);
        Directory.CreateDirectory(root);

        foreach (var (file, content) in files)
            File.WriteAllText(Path.Combine(root, file), content);

        return root;
    }

    private static string ProjectDeclaring(string version) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
          <ItemGroup>
            <PackageReference Include="DevExpress.ExpressApp" Version="{version}" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void ReadsTheProjectTheFolderIsNamedFor()
    {
        var folder = ModuleFolder(
            "Sales.Module",
            ("Sales.Module.csproj", ProjectDeclaring("23.2.5")),
            ("Sales - Backup.Module.csproj", ProjectDeclaring("19.1.4")));

        try
        {
            var project = new LogicExtractor().ExtractFromSourceDirectory(folder);

            // The convention every .NET project follows: the file named after the folder is the
            // project, and a backup beside it is not.
            Assert.Equal("23.2", project.DeclaredDevExpressVersion);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
        }
    }

    [Fact]
    public void AMigrationCandidateBesideTheProjectDoesNotWin()
    {
        // `.Net10.csproj` sorts before `.csproj` under ordinal comparison and after it under a
        // case-insensitive one, which is precisely why the answer must not come from ordering.
        var folder = ModuleFolder(
            "PWPresupuesto.Module",
            ("PWPresupuesto.Module.csproj", ProjectDeclaring("25.1.6")),
            ("PWPresupuesto.Module.Net10.csproj", ProjectDeclaring("26.1.3")));

        try
        {
            var project = new LogicExtractor().ExtractFromSourceDirectory(folder);
            Assert.Equal("25.1", project.DeclaredDevExpressVersion);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
        }
    }

    [Fact]
    public void WithNoNameMatchTheChoiceIsAtLeastTheSameEverywhere()
    {
        // Neither file is named for the folder, so the convention says nothing. Ordinal order is
        // not a better answer than the file system's — it is only the same answer on every machine,
        // which is what stops two people getting different documentation from one repository.
        var folder = ModuleFolder(
            "Module",
            ("Beta.csproj", ProjectDeclaring("24.1.3")),
            ("Alpha.csproj", ProjectDeclaring("22.2.7")));

        try
        {
            var project = new LogicExtractor().ExtractFromSourceDirectory(folder);
            Assert.Equal("22.2", project.DeclaredDevExpressVersion);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
        }
    }

    [Fact]
    public void APackageVersionIsResolvedBeforeItIsReported()
    {
        // The package list is rendered into the documentation, so `$(DevExpressVersion)` reaching a
        // reader is a second, quieter form of the same defect: the answer is there, unresolved.
        var folder = ModuleFolder(
            "Visita.Module",
            ("Visita.Module.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <DevExpressVersion>25.2.7</DevExpressVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="DevExpress.ExpressApp" Version="$(DevExpressVersion)" />
                  </ItemGroup>
                </Project>
                """));

        try
        {
            var project = new LogicExtractor().ExtractFromSourceDirectory(folder);

            Assert.Contains("DevExpress.ExpressApp 25.2.7", project.PackageReferences);
            Assert.Equal("25.2", project.DeclaredDevExpressVersion);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
        }
    }
}
