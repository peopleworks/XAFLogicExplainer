using System.Reflection;
using System.Runtime.InteropServices;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.DxCatalog;

/// <summary>
/// Reads a DevExpress installation and produces the ground-truth catalog.
/// </summary>
public sealed class CatalogBuilder
{
    private const string AttributeBase = "System.Attribute";
    private const string ControllerBase = "DevExpress.ExpressApp.Controller";
    private const string ModuleBase = "DevExpress.ExpressApp.ModuleBase";

    private readonly Action<string> _report;

    /// <summary>Creates a builder.</summary>
    /// <param name="report">Progress callback.</param>
    public CatalogBuilder(Action<string>? report = null) => _report = report ?? (_ => { });

    /// <summary>
    /// Builds a catalog from an installation.
    /// </summary>
    /// <param name="installation">The DevExpress installation to read.</param>
    public XafCatalog Build(DevExpressInstallation installation)
    {
        var documentation = new XmlDocumentationReader();
        foreach (var assembly in installation.XafAssemblies)
            documentation.LoadFor(assembly);

        _report($"Read documentation for {documentation.Count} types.");

        // Everything a type's base chain touches has to be resolvable, which reaches well past the
        // DevExpress folder: System.Object lives in the base runtime, and XAF assemblies also
        // reference ASP.NET Core and WindowsDesktop types. Without those shared frameworks the
        // core DevExpress.ExpressApp assembly itself fails to read -- taking ViewController and
        // most of the attribute surface with it, which is precisely what the catalog exists for.
        var searchPaths = new List<string>(installation.XafAssemblies);
        searchPaths.AddRange(Directory.EnumerateFiles(installation.AssemblyDirectory, "*.dll"));
        searchPaths.AddRange(SharedFrameworkAssemblies());

        // Deduplicate by assembly name, not by path. Each shared framework ships its own copy of
        // the core assemblies, and PathAssemblyResolver rejects a set containing two files with
        // the same simple name. First occurrence wins, which is why the DevExpress assemblies and
        // the base runtime are added before the other frameworks.
        var byAssemblyName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in searchPaths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!byAssemblyName.ContainsKey(name))
                byAssemblyName[name] = path;
        }

        var resolver = new PathAssemblyResolver(byAssemblyName.Values);
        using var context = new MetadataLoadContext(resolver);

        var catalog = new XafCatalog
        {
            DevExpressVersion = installation.Version,
            GeneratedAt = DateTime.UtcNow.ToString("o"),
        };

        foreach (var assemblyPath in installation.XafAssemblies)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            try
            {
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
                var added = Harvest(assembly, assemblyName, catalog, documentation);

                if (added > 0)
                {
                    catalog.Assemblies.Add(assemblyName);
                    _report($"  {assemblyName}: {added} types");
                }
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                // A native or unreadable assembly is skipped rather than aborting the run; the
                // catalog is a best effort over whatever the installation actually contains.
                //
                // Name what was missing. "skipped (FileNotFoundException)" gives a reader nothing
                // to act on, and this is how the core assembly silently vanished from an early run.
                var missing = ex is FileNotFoundException { FileName: { Length: > 0 } file }
                    ? $" — could not resolve {file.Split(',')[0]}"
                    : $" — {ex.Message}";

                _report($"  {assemblyName}: skipped{missing}");
            }
        }

        return catalog;
    }

    /// <summary>
    /// Classifies every public type in an assembly into the catalog.
    /// </summary>
    private static int Harvest(
        Assembly assembly,
        string assemblyName,
        XafCatalog catalog,
        XmlDocumentationReader documentation)
    {
        var added = 0;

        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        foreach (var type in types)
        {
            if (type is null)
                continue;

            // Per type, not per assembly.
            //
            // XAF assemblies reference things delivered by NuGet rather than by the installer --
            // System.ServiceModel.Primitives, Microsoft.EntityFrameworkCore -- which are not in
            // the DevExpress folder or any shared framework. Reading a type that touches one
            // throws, and letting that escape cost the entire assembly: DevExpress.ExpressApp
            // itself, holding ViewController and most of the attribute surface, was being dropped
            // over a handful of unreadable types.
            try
            {
                if (!type.IsPublic)
                    continue;

                if (type.IsInterface)
                {
                    if (type.Name.StartsWith("IModel", StringComparison.Ordinal))
                    {
                        catalog.ModelInterfaces[type.Name] = Describe(type, assemblyName, documentation);
                        added++;
                    }

                    continue;
                }

                if (DerivesFrom(type, AttributeBase))
                {
                    catalog.Attributes[type.Name] = Describe(type, assemblyName, documentation);
                    added++;
                }
                else if (DerivesFrom(type, ControllerBase))
                {
                    var entry = Describe(type, assemblyName, documentation);

                    if (TargetingFromBaseChain(type) is { } targeting)
                    {
                        entry.Targeting = targeting;
                        entry.TargetingSource = "reflection";
                    }

                    catalog.Controllers[type.Name] = entry;
                    added++;
                }
                else if (DerivesFrom(type, ModuleBase))
                {
                    catalog.Modules[type.Name] = Describe(type, assemblyName, documentation);
                    added++;
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException)
            {
                // This one type cannot be read. Every other type in the assembly still can.
            }
        }

        return added;
    }

    private static XafCatalogType Describe(Type type, string assemblyName, XmlDocumentationReader documentation)
    {
        var docs = documentation.Find(type.FullName ?? type.Name);

        return new XafCatalogType
        {
            Name = type.Name,
            Namespace = type.Namespace ?? string.Empty,
            Assembly = StripVersionSuffix(assemblyName),
            BaseType = type.BaseType?.Name,
            IsAbstract = type.IsAbstract,
            Summary = string.IsNullOrWhiteSpace(docs?.Summary) ? null : docs.Summary,
            DocumentationUrl = docs?.DocumentationUrl,
        };
    }

    /// <summary>
    /// Reads whatever targeting the generic base type states, or null when it states none.
    /// </summary>
    /// <remarks>
    /// This is all reflection can offer. A controller written as
    /// <c>ObjectViewController&lt;DetailView, Order&gt;</c> carries its targeting in metadata; one
    /// that assigns <c>TargetViewType</c> in its constructor carries it in IL that assembly
    /// metadata does not expose. In the SystemModule alone the second form outnumbers the first
    /// five to one, which is why the sources pass exists.
    /// <para>
    /// It was being thrown away for free: <c>type.BaseType?.Name</c> drops the generic arguments,
    /// so the cases reflection <em>can</em> answer were reduced to the string "ViewController".
    /// </para>
    /// </remarks>
    private static ControllerTargeting? TargetingFromBaseChain(Type type)
    {
        try
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (!current.IsGenericType)
                    continue;

                var arguments = current.GetGenericArguments();

                // An open generic -- ViewController<T> on a class that is itself generic -- names
                // no particular view.
                if (Array.Exists(arguments, argument => argument.IsGenericParameter))
                    continue;

                var definition = current.GetGenericTypeDefinition().Name;

                if (definition == "ObjectViewController`2" && arguments.Length == 2)
                {
                    return new ControllerTargeting
                    {
                        TypeOfView = ControllerTargetingReader.NormalizeViewType(arguments[0].Name),
                        TargetObjectType = arguments[1].Name,
                    };
                }

                if (definition == "ViewController`1" && arguments.Length == 1)
                {
                    // ViewController<View> restricts nothing, and saying so would imply the
                    // constructor was read too.
                    if (ControllerTargetingReader.NormalizeViewType(arguments[0].Name) is not { } viewType)
                        return null;

                    return new ControllerTargeting { TypeOfView = viewType };
                }
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            // A base type in an assembly that is not present. Nothing more can be said about it.
        }

        return null;
    }

    /// <summary>
    /// Walks the base chain looking for a type by full name.
    /// </summary>
    /// <remarks>
    /// Compared by name rather than with <c>typeof</c>. Types loaded through a
    /// <see cref="MetadataLoadContext"/> are distinct from the running process's types, so
    /// <c>type.IsSubclassOf(typeof(Attribute))</c> is false even for an attribute.
    /// </remarks>
    private static bool DerivesFrom(Type type, string baseTypeFullName)
    {
        try
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.FullName == baseTypeFullName)
                    return true;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            // A base type in an assembly that is not present. Nothing more can be said about it.
        }

        return false;
    }

    /// <summary>Turns "DevExpress.ExpressApp.v26.1" into "DevExpress.ExpressApp".</summary>
    private static string StripVersionSuffix(string assemblyName)
    {
        var marker = assemblyName.LastIndexOf(".v", StringComparison.Ordinal);
        return marker > 0 ? assemblyName[..marker] : assemblyName;
    }

    /// <summary>
    /// Gathers every assembly from the installed shared frameworks.
    /// </summary>
    /// <remarks>
    /// The running runtime directory covers Microsoft.NETCore.App only. XAF also spans ASP.NET
    /// Core and WindowsDesktop, which sit beside it under <c>dotnet/shared</c>, so the newest
    /// build of each is included. Being generous here costs a directory listing; being stingy
    /// costs whole assemblies.
    /// </remarks>
    private static IEnumerable<string> SharedFrameworkAssemblies()
    {
        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

        foreach (var file in SafeEnumerate(runtimeDirectory))
            yield return file;

        // .../shared/Microsoft.NETCore.App/10.0.10/ -> .../shared/
        var sharedRoot = Directory.GetParent(runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Parent;

        if (sharedRoot is null || !sharedRoot.Exists)
            yield break;

        foreach (var framework in sharedRoot.EnumerateDirectories())
        {
            var newest = framework
                .EnumerateDirectories()
                .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (newest is null)
                continue;

            foreach (var file in SafeEnumerate(newest.FullName))
                yield return file;
        }
    }

    private static IEnumerable<string> SafeEnumerate(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.dll");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
