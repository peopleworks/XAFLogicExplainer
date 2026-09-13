using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model.NodeGenerators;

namespace Library.Module;

/// <summary>
/// The XAF module. Two business classes are named <c>Tag</c>, and XAF names every generated view
/// after the class, so without a prefix both would claim <c>Tag_ListView</c> and the application
/// would stop at startup with a <c>DuplicateModelNodeIdException</c>.
/// </summary>
public sealed class LibraryModule : ModuleBase
{
    public LibraryModule()
    {
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.SystemModule.SystemModule));
    }

    public override void CustomizeTypesInfo(ITypesInfo typesInfo)
    {
        base.CustomizeTypesInfo(typesInfo);

        // The catalog's views become CatalogTag_ListView, CatalogTag_DetailView and so on.
        ModelNodesGeneratorSettings.SetIdPrefix(typeof(BusinessObjects.Catalog.Tag), "CatalogTag");
    }
}
