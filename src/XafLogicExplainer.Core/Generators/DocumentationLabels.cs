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

    /// <summary>How a rule that declares no criteria is described: in XAF it is always active.</summary>
    public string Always { get; init; } = "";

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

    // Walkthrough — one business process, traced

    /// <summary>Name of the document: the traced account of one process.</summary>
    public string Walkthrough { get; init; } = "";

    /// <summary>Said when the seed matched nothing at all.</summary>
    public string WalkthroughNotFound { get; init; } = "";

    /// <summary>Opens the sentence naming what the walk started from.</summary>
    public string StartedFrom { get; init; } = "";

    /// <summary>How far it went when it ran out of code before the limit. {0} nodes, {1} depth.</summary>
    public string ReachedAndFinished { get; init; } = "";

    /// <summary>How far it went when the limit stopped it. {0} nodes, {1} depth.</summary>
    public string ReachedAndStopped { get; init; } = "";

    /// <summary>
    /// How far it went when it finished inside the limit but left calls it could not follow.
    /// {0} nodes, {1} depth.
    /// </summary>
    /// <remarks>
    /// Its own sentence because the plain one would contradict the section below it: a walk that
    /// stopped at a virtual call did not run out of code, it ran out of code it could decide.
    /// </remarks>
    public string ReachedAndBlocked { get; init; } = "";

    /// <summary>Heading over the diagram.</summary>
    public string Flow { get; init; } = "";

    /// <summary>Heading over the list of everything the walk reached.</summary>
    /// <remarks>
    /// Its own section because the steps cite their target, so a node that is only ever the source
    /// of a step -- a controller, typically -- would otherwise appear in the document with no place
    /// attached to it at all.
    /// </remarks>
    public string WhatTakesPart { get; init; } = "";

    /// <summary>Heading over the ordered account.</summary>
    public string StepByStep { get; init; } = "";

    /// <summary>Heading over the calls the walk saw and could not follow.</summary>
    public string CouldNotFollow { get; init; } = "";

    /// <summary>Said when every call resolved, so the absence of a list means something.</summary>
    public string EverythingResolved { get; init; } = "";

    /// <summary>Joins an unfollowed call to the code that makes it.</summary>
    public string CalledFrom { get; init; } = "";

    /// <summary>Introduces the declarations an unfollowed call might have reached.</summary>
    public string CouldBe { get; init; } = "";

    /// <summary>Heading over the bounds, which a document has to state to be read correctly.</summary>
    public string WhatThisIsNot { get; init; } = "";

    /// <summary>The depth bound, when it stopped the walk. {0} is the depth.</summary>
    public string BoundHit { get; init; } = "";

    /// <summary>The depth bound, when the walk finished inside it. {0} is the depth.</summary>
    public string BoundNotHit { get; init; } = "";

    /// <summary>That entity-to-entity relationships are deliberately not walked.</summary>
    public string BoundNoRelationships { get; init; } = "";

    /// <summary>That a controller reached from its own action does not fan out to its siblings.</summary>
    public string BoundNoSiblings { get; init; } = "";

    /// <summary>That methods come from controllers, so a service class is not walked into.</summary>
    public string BoundControllersOnly { get; init; } = "";

    /// <summary>Verb for a controller declaring an action or a method.</summary>
    public string StepDeclares { get; init; } = "";

    /// <summary>Verb for code invoking a method.</summary>
    public string StepCalls { get; init; } = "";

    /// <summary>Verb for code naming an entity.</summary>
    public string StepTouches { get; init; } = "";

    /// <summary>Verb for a controller being activated on an entity.</summary>
    public string StepTargets { get; init; } = "";

    /// <summary>Verb for an entity carrying a rule.</summary>
    public string StepGoverns { get; init; } = "";

    /// <summary>What an action is called, with its article where the language needs one.</summary>
    public string KindAction { get; init; } = "";

    /// <summary>What a method is called.</summary>
    public string KindMethod { get; init; } = "";

    /// <summary>What a controller is called.</summary>
    public string KindController { get; init; } = "";

    /// <summary>What an entity is called.</summary>
    public string KindEntity { get; init; } = "";

    /// <summary>What a validation rule is called.</summary>
    public string KindValidationRule { get; init; } = "";

    /// <summary>What an appearance rule is called.</summary>
    public string KindAppearanceRule { get; init; } = "";

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
        Always = "siempre",

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

        Walkthrough = "Recorrido",
        WalkthroughNotFound = "Nada en esta aplicacion coincide con ese nombre.",
        StartedFrom = "Parte de",
        ReachedAndFinished =
            "Nodos en esta rebanada: {0}. Limite de profundidad: {1}, no alcanzado — el recorrido se "
            + "quedo antes sin codigo que seguir.",
        ReachedAndStopped =
            "Nodos en esta rebanada: {0}. Limite de profundidad: {1}, alcanzado — esto es una vista "
            + "del proceso, no el proceso completo.",
        ReachedAndBlocked =
            "Nodos en esta rebanada: {0}. Limite de profundidad: {1}, no alcanzado — el recorrido se "
            + "quedo sin llamadas que pudiera resolver, y las que no pudo estan listadas mas abajo.",
        Flow = "Flujo",
        WhatTakesPart = "Lo que participa",
        StepByStep = "Paso a paso",
        CouldNotFollow = "Lo que este recorrido no pudo seguir",
        EverythingResolved = "Nada. Cada llamada de esta rebanada resolvio a una sola declaracion.",
        CalledFrom = "llamada desde",
        CouldBe = "Podria ser",
        WhatThisIsNot = "Lo que este recorrido es, y lo que no",
        BoundHit =
            "Siguio el proceso hasta una profundidad de {0} y se detuvo ahi, asi que lo que este mas "
            + "lejos falta por diseno.",
        BoundNotHit = "Tenia permitida una profundidad de {0} y no la necesito toda.",
        BoundNoRelationships =
            "Las relaciones entre entidades no se siguen. Son lo que hace que una rebanada alcance la "
            + "aplicacion entera; el mapa de entidades responde esa pregunta.",
        BoundNoSiblings =
            "Un controlador alcanzado desde una de sus propias acciones aporta lo que esa accion "
            + "ejecuta, no sus otras acciones.",
        BoundControllersOnly =
            "Los metodos se extraen de los controladores, asi que un calculo que vive en una clase de "
            + "servicio aparece como una llamada que no se pudo seguir, no como un paso.",
        StepDeclares = "declara",
        StepCalls = "llama a",
        StepTouches = "trabaja con",
        StepTargets = "se activa para",
        StepGoverns = "lleva la regla",
        KindAction = "accion",
        KindMethod = "metodo",
        KindController = "controlador",
        KindEntity = "entidad",
        KindValidationRule = "regla de validacion",
        KindAppearanceRule = "regla de apariencia",
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
        Always = "always",

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

        Walkthrough = "Walkthrough",
        WalkthroughNotFound = "Nothing in this application matched that name.",
        StartedFrom = "Started from",
        ReachedAndFinished =
            "Nodes in this slice: {0}. Depth limit: {1}, not reached — the walk ran out of code to "
            + "follow first.",
        ReachedAndStopped =
            "Nodes in this slice: {0}. Depth limit: {1}, reached — this is a view of the process, not "
            + "the whole of it.",
        ReachedAndBlocked =
            "Nodes in this slice: {0}. Depth limit: {1}, not reached — the walk ran out of calls it "
            + "could resolve, and the ones it could not are listed below.",
        Flow = "Flow",
        WhatTakesPart = "What takes part",
        StepByStep = "Step by step",
        CouldNotFollow = "What this walk could not follow",
        EverythingResolved = "Nothing. Every call in this slice resolved to a single declaration.",
        CalledFrom = "called from",
        CouldBe = "Could be",
        WhatThisIsNot = "What this walk is, and is not",
        BoundHit =
            "It followed the process to a depth of {0} and stopped there, so anything further out is "
            + "missing by design.",
        BoundNotHit = "It was allowed a depth of {0} and did not need all of it.",
        BoundNoRelationships =
            "Relationships between entities are not followed. They are what makes a slice reach the "
            + "whole application; the entity map answers that question instead.",
        BoundNoSiblings =
            "A controller reached from one of its own actions contributes what that action runs, not "
            + "its other actions.",
        BoundControllersOnly =
            "Methods are extracted from controllers, so a calculation living in a plain service class "
            + "appears as a call that could not be followed rather than as a step.",
        StepDeclares = "declares",
        StepCalls = "calls",
        StepTouches = "works with",
        StepTargets = "is activated for",
        StepGoverns = "carries the rule",
        KindAction = "action",
        KindMethod = "method",
        KindController = "controller",
        KindEntity = "entity",
        KindValidationRule = "validation rule",
        KindAppearanceRule = "appearance rule",
    };
}
