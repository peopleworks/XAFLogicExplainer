namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// Strongly-typed localized label set used by documentation generators.
/// </summary>
public class DocumentationLabels
{
    // Section titles
    public string FunctionalDocumentation { get; init; } = "";
    public string SystemOverview { get; init; } = "";
    public string BusinessEntities { get; init; } = "";
    public string ControllersAndActions { get; init; } = "";
    public string BusinessRules { get; init; } = "";
    public string ConfigurationAndSeedData { get; init; } = "";
    public string NavigationStructure { get; init; } = "";

    /// <summary>Heading for the screen inventory.</summary>
    public string Screens { get; init; } = "";
    public string ModelEditorCustomizations { get; init; } = "";
    public string FullDocumentation { get; init; } = "";

    // Overview
    public string Summary { get; init; } = "";
    public string Project { get; init; } = "";
    public string Framework { get; init; } = "";
    public string BusinessEntitiesCount { get; init; } = "";
    public string ControllersCount { get; init; } = "";
    public string NavigationGroups { get; init; } = "";
    public string CustomViewsModelEditor { get; init; } = "";
    public string UIStyle { get; init; } = "";
    public string MainPackages { get; init; } = "";
    public string EntityRelationshipMap { get; init; } = "";
    public string Composition { get; init; } = "";

    // Entities
    public string BaseType { get; init; } = "";
    public string CaptionModelEditor { get; init; } = "";
    public string NavigationGroup { get; init; } = "";
    public string DisplayProperty { get; init; } = "";
    public string Persistent { get; init; } = "";
    public string Cloneable { get; init; } = "";
    public string Properties { get; init; } = "";
    public string Relationships { get; init; } = "";
    public string ValidationRules { get; init; } = "";
    public string AppearanceRules { get; init; } = "";

    // Property table headers
    public string Property { get; init; } = "";
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    public string Required { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Computed { get; init; } = "";
    public string Format { get; init; } = "";
    public string Default { get; init; } = "";
    public string Filter { get; init; } = "";

    // Relationships
    public string OneToMany { get; init; } = "";
    public string ManyToOne { get; init; } = "";
    public string CompositionAggregation { get; init; } = "";

    // Controllers
    public string TargetEntity { get; init; } = "";
    public string ViewType { get; init; } = "";
    public string ReferencedEntities { get; init; } = "";
    public string Actions { get; init; } = "";
    public string Category { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Confirmation { get; init; } = "";
    public string Tooltip { get; init; } = "";
    public string ExecutionLogic { get; init; } = "";
    public string MoreLines { get; init; } = "";
    public string MainMethods { get; init; } = "";
    public string BusinessLogicSummary { get; init; } = "";
    public string ActionBusinessLogic { get; init; } = "";

    // Business rules
    public string ConditionalBehaviorRules { get; init; } = "";
    public string Visibility { get; init; } = "";
    public string Enabled { get; init; } = "";
    public string Fields { get; init; } = "";
    public string ComputedPropertiesDerived { get; init; } = "";
    public string ExplicitBusinessRules { get; init; } = "";
    public string ClassLevel { get; init; } = "";
    public string When { get; init; } = "";

    // Configuration
    public string MainModule { get; init; } = "";
    public string Class { get; init; } = "";
    public string RequiredModules { get; init; } = "";
    public string RegisteredTypes { get; init; } = "";
    public string SeedData { get; init; } = "";
    public string SeedDataDescription { get; init; } = "";
    public string Entity { get; init; } = "";
    public string Method { get; init; } = "";
    public string SourceCodeOf { get; init; } = "";

    // Navigation
    public string ApplicationMenu { get; init; } = "";
    public string MainProperties { get; init; } = "";
    public string WithoutNavigationGroup { get; init; } = "";

    // Model Editor
    public string ApplicationOptions { get; init; } = "";
    public string UITypeLabel { get; init; } = "";
    public string FormStyleLabel { get; init; } = "";
    public string VisualTheme { get; init; } = "";
    public string RequiredFieldMark { get; init; } = "";
    public string BOModelCustomizations { get; init; } = "";
    public string Caption { get; init; } = "";
    public string Other { get; init; } = "";
    public string ViewCustomizations { get; init; } = "";
    public string Editable { get; init; } = "";
    public string AllowDelete { get; init; } = "";
    public string AllowCreate { get; init; } = "";
    public string FilterCriteria { get; init; } = "";
    public string CustomEditor { get; init; } = "";
    public string ConfiguredColumns { get; init; } = "";
    public string Order { get; init; } = "";
    public string Width { get; init; } = "";
    public string Sorting { get; init; } = "";
    public string PredefinedFilters { get; init; } = "";
    public string NavigationModelEditor { get; init; } = "";
    public string Style { get; init; } = "";
    public string StartupView { get; init; } = "";
    public string RegisteredSchemaModules { get; init; } = "";
    public string ProcessedXafmlFiles { get; init; } = "";

    // Common
    public string Yes { get; init; } = "";
    public string No { get; init; } = "";

    // Section descriptions (templates with {0} for project name)
    public string OverviewDescription { get; init; } = "";
    public string EntitiesDescription { get; init; } = "";
    public string ControllersDescription { get; init; } = "";
    public string BusinessRulesDescription { get; init; } = "";
    public string ConfigurationDescription { get; init; } = "";
    public string NavigationDescription { get; init; } = "";

    /// <summary>Description of the screen inventory section.</summary>
    public string ScreensDescription { get; init; } = "";
    public string ModelEditorDescription { get; init; } = "";
    public string FullDocDescription { get; init; } = "";

    /// <summary>
    /// Gets a language-specific label set.
    /// </summary>
    /// <param name="languageCode">Language code ("en" or "es").</param>
    /// <returns>Initialized label set for requested language.</returns>
    public static DocumentationLabels ForLanguage(string languageCode) => languageCode switch
    {
        "en" => English(),
        _ => Spanish()
    };

    /// <summary>
    /// Returns Spanish label values.
    /// </summary>
    public static DocumentationLabels Spanish() => new()
    {
        FunctionalDocumentation = "Documentacion Funcional",
        SystemOverview = "Vision General del Sistema",
        BusinessEntities = "Entidades de Negocio",
        ControllersAndActions = "Controladores y Acciones",
        BusinessRules = "Reglas de Negocio",
        ConfigurationAndSeedData = "Configuracion y Datos Semilla",
        NavigationStructure = "Estructura de Navegacion",
        Screens = "Pantallas",
        ModelEditorCustomizations = "Personalizaciones del Model Editor",
        FullDocumentation = "Documentacion Funcional Completa",

        Summary = "Resumen",
        Project = "Proyecto",
        Framework = "Framework",
        BusinessEntitiesCount = "Entidades de negocio",
        ControllersCount = "Controladores",
        NavigationGroups = "Grupos de navegacion",
        CustomViewsModelEditor = "Vistas personalizadas (Model Editor)",
        UIStyle = "Estilo UI",
        MainPackages = "Paquetes Principales",
        EntityRelationshipMap = "Mapa de Relaciones entre Entidades",
        Composition = "composicion",

        BaseType = "Tipo base",
        CaptionModelEditor = "Caption (Model Editor)",
        NavigationGroup = "Grupo de navegacion",
        DisplayProperty = "Propiedad de display",
        Persistent = "Persistente",
        Cloneable = "Clonable",
        Properties = "Propiedades",
        Relationships = "Relaciones",
        ValidationRules = "Reglas de Validacion",
        AppearanceRules = "Reglas de Apariencia",

        Property = "Propiedad",
        Type = "Tipo",
        Description = "Descripcion",
        Required = "Requerido",
        Notes = "Notas",
        Computed = "Calculado",
        Format = "Formato",
        Default = "Default",
        Filter = "Filtro",

        OneToMany = "Uno a muchos",
        ManyToOne = "Muchos a uno",
        CompositionAggregation = "composicion/agregacion",

        TargetEntity = "Entidad objetivo",
        ViewType = "Tipo de vista",
        ReferencedEntities = "Entidades referenciadas",
        Actions = "Acciones",
        Category = "Categoria",
        Icon = "Icono",
        Confirmation = "Confirmacion",
        Tooltip = "Tooltip",
        ExecutionLogic = "Logica de ejecucion",
        MoreLines = "lineas mas",
        MainMethods = "Metodos Principales",
        BusinessLogicSummary = "Resumen de Logica de Negocio",
        ActionBusinessLogic = "Logica de negocio",

        ConditionalBehaviorRules = "Reglas de Comportamiento Condicional",
        Visibility = "visibilidad",
        Enabled = "habilitado",
        Fields = "campos",
        ComputedPropertiesDerived = "Propiedades Calculadas (Logica Derivada)",
        ExplicitBusinessRules = "Reglas de Negocio Explicitas",
        ClassLevel = "clase",
        When = "cuando",

        MainModule = "Modulo Principal",
        Class = "Clase",
        RequiredModules = "Modulos Requeridos",
        RegisteredTypes = "Tipos Registrados",
        SeedData = "Datos Semilla (Inicializacion)",
        SeedDataDescription = "Los siguientes datos se crean automaticamente en la primera ejecucion:",
        Entity = "Entidad",
        Method = "Metodo",
        SourceCodeOf = "Codigo fuente de",

        ApplicationMenu = "Menu de la Aplicacion",
        MainProperties = "Propiedades principales",
        WithoutNavigationGroup = "Sin Grupo de Navegacion",

        ApplicationOptions = "Opciones de la Aplicacion",
        UITypeLabel = "Tipo de UI",
        FormStyleLabel = "Estilo de formulario",
        VisualTheme = "Tema visual",
        RequiredFieldMark = "Marca de campo requerido",
        BOModelCustomizations = "Personalizaciones de Clases (BOModel)",
        Caption = "Caption",
        Other = "Otros",
        ViewCustomizations = "Personalizaciones de Vistas",
        Editable = "Editable",
        AllowDelete = "Permite borrar",
        AllowCreate = "Permite crear",
        FilterCriteria = "Criterio de filtro",
        CustomEditor = "Editor personalizado",
        ConfiguredColumns = "Columnas configuradas",
        Order = "Orden",
        Width = "Ancho",
        Sorting = "Ordenamiento",
        PredefinedFilters = "Filtros predefinidos",
        NavigationModelEditor = "Navegacion (Model Editor)",
        Style = "Estilo",
        StartupView = "Vista inicial",
        RegisteredSchemaModules = "Modulos de Esquema Registrados",
        ProcessedXafmlFiles = "Archivos XAFML Procesados",

        Yes = "Si",
        No = "No",

        OverviewDescription = "Vision general del sistema {0}: estructura, navegacion y relaciones",
        EntitiesDescription = "Todas las entidades de negocio de {0} con propiedades, relaciones y reglas",
        ControllersDescription = "Controladores XAF de {0}: acciones, logica de negocio y flujos de trabajo",
        BusinessRulesDescription = "Reglas de negocio de {0}: validaciones, comportamiento condicional, propiedades calculadas",
        ConfigurationDescription = "Configuracion del modulo {0}: modulos requeridos, tipos registrados, datos iniciales",
        NavigationDescription = "Estructura de menu y navegacion de {0}",
        ScreensDescription = "Cada vista de {0} y los controladores que XAF activa en ella",
        ModelEditorDescription = "Personalizaciones del Model Editor de {0}: vistas, columnas, filtros, opciones de UI",
        FullDocDescription = "Documentacion funcional completa del proyecto {0} auto-generada por XAF Logic Explainer",
    };

    /// <summary>
    /// Returns English label values.
    /// </summary>
    public static DocumentationLabels English() => new()
    {
        FunctionalDocumentation = "Functional Documentation",
        SystemOverview = "System Overview",
        BusinessEntities = "Business Entities",
        ControllersAndActions = "Controllers and Actions",
        BusinessRules = "Business Rules",
        ConfigurationAndSeedData = "Configuration and Seed Data",
        NavigationStructure = "Navigation Structure",
        Screens = "Screens",
        ModelEditorCustomizations = "Model Editor Customizations",
        FullDocumentation = "Complete Functional Documentation",

        Summary = "Summary",
        Project = "Project",
        Framework = "Framework",
        BusinessEntitiesCount = "Business entities",
        ControllersCount = "Controllers",
        NavigationGroups = "Navigation groups",
        CustomViewsModelEditor = "Custom views (Model Editor)",
        UIStyle = "UI Style",
        MainPackages = "Main Packages",
        EntityRelationshipMap = "Entity Relationship Map",
        Composition = "composition",

        BaseType = "Base type",
        CaptionModelEditor = "Caption (Model Editor)",
        NavigationGroup = "Navigation group",
        DisplayProperty = "Display property",
        Persistent = "Persistent",
        Cloneable = "Cloneable",
        Properties = "Properties",
        Relationships = "Relationships",
        ValidationRules = "Validation Rules",
        AppearanceRules = "Appearance Rules",

        Property = "Property",
        Type = "Type",
        Description = "Description",
        Required = "Required",
        Notes = "Notes",
        Computed = "Computed",
        Format = "Format",
        Default = "Default",
        Filter = "Filter",

        OneToMany = "One to many",
        ManyToOne = "Many to one",
        CompositionAggregation = "composition/aggregation",

        TargetEntity = "Target entity",
        ViewType = "View type",
        ReferencedEntities = "Referenced entities",
        Actions = "Actions",
        Category = "Category",
        Icon = "Icon",
        Confirmation = "Confirmation",
        Tooltip = "Tooltip",
        ExecutionLogic = "Execution logic",
        MoreLines = "more lines",
        MainMethods = "Main Methods",
        BusinessLogicSummary = "Business Logic Summary",
        ActionBusinessLogic = "Business logic",

        ConditionalBehaviorRules = "Conditional Behavior Rules",
        Visibility = "visibility",
        Enabled = "enabled",
        Fields = "fields",
        ComputedPropertiesDerived = "Computed Properties (Derived Logic)",
        ExplicitBusinessRules = "Explicit Business Rules",
        ClassLevel = "class",
        When = "when",

        MainModule = "Main Module",
        Class = "Class",
        RequiredModules = "Required Modules",
        RegisteredTypes = "Registered Types",
        SeedData = "Seed Data (Initialization)",
        SeedDataDescription = "The following data is created automatically on first run:",
        Entity = "Entity",
        Method = "Method",
        SourceCodeOf = "Source code of",

        ApplicationMenu = "Application Menu",
        MainProperties = "Main properties",
        WithoutNavigationGroup = "Without Navigation Group",

        ApplicationOptions = "Application Options",
        UITypeLabel = "UI Type",
        FormStyleLabel = "Form style",
        VisualTheme = "Visual theme",
        RequiredFieldMark = "Required field mark",
        BOModelCustomizations = "Class Customizations (BOModel)",
        Caption = "Caption",
        Other = "Other",
        ViewCustomizations = "View Customizations",
        Editable = "Editable",
        AllowDelete = "Allow delete",
        AllowCreate = "Allow create",
        FilterCriteria = "Filter criteria",
        CustomEditor = "Custom editor",
        ConfiguredColumns = "Configured columns",
        Order = "Order",
        Width = "Width",
        Sorting = "Sorting",
        PredefinedFilters = "Predefined filters",
        NavigationModelEditor = "Navigation (Model Editor)",
        Style = "Style",
        StartupView = "Startup view",
        RegisteredSchemaModules = "Registered Schema Modules",
        ProcessedXafmlFiles = "Processed XAFML Files",

        Yes = "Yes",
        No = "No",

        OverviewDescription = "System overview of {0}: structure, navigation and relationships",
        EntitiesDescription = "All business entities of {0} with properties, relationships and rules",
        ControllersDescription = "XAF controllers of {0}: actions, business logic and workflows",
        BusinessRulesDescription = "Business rules of {0}: validations, conditional behavior, computed properties",
        ConfigurationDescription = "Module configuration of {0}: required modules, registered types, initial data",
        NavigationDescription = "Menu and navigation structure of {0}",
        ScreensDescription = "Every view in {0} and the controllers XAF activates on each",
        ModelEditorDescription = "Model Editor customizations of {0}: views, columns, filters, UI options",
        FullDocDescription = "Complete functional documentation of project {0} auto-generated by XAF Logic Explainer",
    };
}
