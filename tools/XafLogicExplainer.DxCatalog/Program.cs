using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.DxCatalog;

// ============================================================
// xafcatalog - builds the XAF ground-truth catalog
//
// Reads a DevExpress installation this machine is licensed for and writes a catalog of framework
// types to ~/.xaflogic/catalog/. Nothing derived from DevExpress is written into the repository.
// ============================================================

var explicitPath = GetArg("--path") ?? GetArg("-p");
var explicitSources = GetArg("--sources") ?? GetArg("-s");
var outputDirectory = GetArg("--out") ?? GetArg("-o");
var quiet = args.Contains("--quiet") || args.Contains("-q");

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
        xafcatalog - build the XAF ground-truth catalog

        Reads the DevExpress installation this machine is licensed for and records what the XAF
        framework itself provides: its attributes, controllers, model interfaces and modules.
        Extraction uses it to tell your application's own logic apart from the framework's.

        Usage:
          xafcatalog [--path <DevExpress directory>] [--sources <directory>]
                     [--out <directory>] [--quiet]

        Options:
          --path,   -p  DevExpress installation or assembly directory. Searched for if omitted.
          --sources, -s Components/Sources directory. Found beside the assemblies if omitted.
          --out,    -o  Where to write. Defaults to ~/.xaflogic/catalog/.
          --quiet,  -q  Only print the resulting path.

        Framework controllers declare where they activate inside their constructors, which
        assembly metadata does not carry. With the DevExpress source component installed the
        catalog reads those constructors and can say which built-in controllers run on a view;
        without it, only the few that state their target in a generic base type are known.

        The catalog is written outside any repository, because it is derived from licensed
        software. It is optional: without one, extraction behaves exactly as it does today.
        """);
    return 0;
}

void Report(string message)
{
    if (!quiet)
        Console.WriteLine(message);
}

Report("Locating a DevExpress installation...");

var installation = DevExpressInstallation.Locate(explicitPath, explicitSources);

if (installation is null)
{
    Console.Error.WriteLine(
        "No DevExpress installation found. Pass --path <directory> pointing at your installation " +
        "(or at the folder containing DevExpress.ExpressApp*.dll).");
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        "This tool requires a licensed DevExpress installation. XAF Logic Explainer works without " +
        "one; the catalog only makes its output more precise.");
    return 1;
}

Report($"DevExpress {installation.Version}");
Report($"  {installation.AssemblyDirectory}");
Report($"  {installation.XafAssemblies.Count} XAF assemblies");
Report("");

var catalog = new CatalogBuilder(Report).Build(installation);

if (installation.SourceDirectory is { } sourceDirectory)
{
    Report("");
    Report($"Reading constructors from {sourceDirectory}");
    new SourceTargetingReader(Report).Apply(catalog, sourceDirectory);
}

// Targeting is inherited: a controller whose base class does the targeting states none of its own.
// Resolving it here means the catalog stores the effective answer.
var inherited = ControllerTargetingResolver.Resolve(catalog);

if (inherited > 0)
    Report($"  {inherited} more restricted once base classes were followed");

var path = XafCatalogStore.Save(catalog, outputDirectory);

if (quiet)
{
    Console.WriteLine(path);
    return 0;
}

Console.WriteLine();
Console.WriteLine($"Catalogued {catalog.TypeCount} framework types:");
Console.WriteLine($"  {catalog.Attributes.Count,5} attributes");
Console.WriteLine($"  {catalog.Controllers.Count,5} controllers");
Console.WriteLine($"  {catalog.ModelInterfaces.Count,5} model interfaces");
Console.WriteLine($"  {catalog.Modules.Count,5} modules");
Console.WriteLine();

// Say plainly how much of the framework's activation behavior is actually known. A catalog built
// without the sources answers "which built-in controllers run on this view" for a handful of
// controllers and guesses at none of the rest -- and a reader who is not told that will read the
// gap as an absence.
var fromSources = catalog.Controllers.Values.Count(c => c.TargetingSource == "sources");
var fromReflection = catalog.Controllers.Values.Count(c => c.TargetingSource == "reflection");
var unknown = catalog.Controllers.Count - fromSources - fromReflection;

Console.WriteLine("Controller targeting — where each one activates:");
Console.WriteLine($"  {fromSources,5} read from source, complete");
Console.WriteLine($"  {fromReflection,5} from the generic base type only, a lower bound");
Console.WriteLine($"  {unknown,5} unknown");

if (installation.SourceDirectory is null)
{
    Console.WriteLine();
    Console.WriteLine(
        "No DevExpress sources found, so most targeting is unknown. Install the source code " +
        "component, or pass --sources <Components/Sources>, to complete it.");
}

Console.WriteLine();
Console.WriteLine($"Written to {path}");
Console.WriteLine();
Console.WriteLine("Extraction picks this up automatically from now on.");

return 0;

string? GetArg(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
