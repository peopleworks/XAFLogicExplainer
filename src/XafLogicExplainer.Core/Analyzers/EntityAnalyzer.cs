using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Interfaces;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Parses C# business object classes and extracts entity metadata, rules, and relationships.
/// </summary>
public class EntityAnalyzer : IEntityAnalyzer
{
    /// <summary>
    /// Analyzes entity candidates and returns extracted business entities.
    /// </summary>
    /// <param name="sourceDirectory">Project root directory.</param>
    /// <param name="options">Extraction configuration.</param>
    /// <returns>Entity extraction results.</returns>
    public List<ExtractedEntity> AnalyzeEntities(string sourceDirectory, ExtractionOptions options)
    {
        var entities = new List<ExtractedEntity>();
        var csFiles = FindFiles(sourceDirectory, options.BusinessObjectPatterns, options.ExcludePatterns).ToList();

        // Parsed once and kept, because the DbSet roster has to be known before the first class is
        // classified and re-parsing every file to build it costs more than holding the trees.
        //
        // Ordered, because the directory hands them over in whatever order the file system keeps
        // them, and that is not the same order on two machines: NTFS compares names without case,
        // ext4 by byte, so `Shipment.Generated.cs` sorts after `Shipment.cs` on one and before it
        // on the other. Anything downstream that takes the first of something then answers
        // differently on a laptop than in CI, in a document whose value is that it can be
        // regenerated and compared.
        var parsedFiles = csFiles
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(file => (File: file, Root: CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetRoot()))
            .ToList();

        var roster = DbSetRoster.Read(parsedFiles.Select(parsed => parsed.Root));

        // Resolved after the parse, because the roster is the best evidence there is and it only
        // exists once the trees do.
        var ormType = options.Orm == OrmType.Auto
            ? DetectOrmType(parsedFiles.Select(parsed => parsed.Root), roster)
            : options.Orm;
        options.ResolvedOrm = ormType;

        foreach (var (file, root) in parsedFiles)
        {
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classDeclarations)
            {
                if (IsXafBusinessObject(classDecl, options.BaseTypeNames)
                    || roster.Registers(GetNamespace(classDecl), classDecl.Identifier.Text))
                {
                    var entity = ExtractEntity(classDecl, file, options);
                    entities.Add(entity);
                }
            }
        }

        // One entity per class, not per declaration -- a partial split across files is one thing.
        entities = MergePartialDeclarations(entities);

        // Post-extraction: infer EF Core relationships from navigation properties
        if (ormType == OrmType.EfCore)
            InferEfCoreRelationships(entities);

        return entities;
    }

    /// <summary>
    /// Extracts one business entity from a class declaration.
    /// </summary>
    private static ExtractedEntity ExtractEntity(ClassDeclarationSyntax classDecl, string filePath, ExtractionOptions options)
    {
        var entity = new ExtractedEntity
        {
            ClassName = classDecl.Identifier.Text,
            Namespace = GetNamespace(classDecl),
            FilePath = filePath,
            BaseType = GetBaseTypeName(classDecl),
            BaseTypes = GetBaseTypeNames(classDecl),
            Description = GetAttributeStringArg(classDecl, "Description"),
            NavigationGroup = GetAttributeStringArg(classDecl, "NavigationItem"),
            DefaultProperty = GetAttributeStringArg(classDecl, "XafDefaultProperty")
                              ?? GetAttributeStringArg(classDecl, "DefaultProperty"),
            IsDefaultClassOptions = HasAttribute(classDecl, "DefaultClassOptions"),
            IsPersistent = !HasAttribute(classDecl, "NonPersistent")
                           && !HasAttribute(classDecl, "DomainComponent")
                           && !HasAttribute(classDecl, "NotMapped"),
        };

        // Extract properties
        foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (IsInfrastructureProperty(prop.Identifier.Text))
                continue;

            // `object ISecurityUserLoginInfo.User => User;` is the same property seen through an
            // interface, not a second one. Listing both puts a duplicate row in every rendering,
            // with the explicit form typed as `object` — which reads as a modelling mistake the
            // team did not make.
            if (prop.ExplicitInterfaceSpecifier is not null)
                continue;

            var extracted = ExtractProperty(prop);
            if (extracted != null)
                entity.Properties.Add(extracted);
        }

        // Extract relationships from properties (Association attribute)
        foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var rel = ExtractRelationship(prop);
            if (rel != null)
                entity.Relationships.Add(rel);
        }

        // Extract validation rules from class-level attributes
        entity.ValidationRules.AddRange(ExtractValidationRules(classDecl));

        // Extract appearance rules from class-level attributes
        entity.AppearanceRules.AddRange(ExtractAppearanceRules(classDecl));

        // Extract comments
        if (options.IncludeComments)
        {
            entity.SourceComments.AddRange(ExtractComments(classDecl));
        }

        return entity;
    }

    /// <summary>
    /// Extracts one property definition from syntax metadata.
    /// </summary>
    private static ExtractedProperty? ExtractProperty(PropertyDeclarationSyntax prop)
    {
        var name = prop.Identifier.Text;
        var typeName = prop.Type.ToString();

        var extracted = new ExtractedProperty
        {
            Name = name,
            TypeName = typeName,
            Description = GetAttributeStringArg(prop, "Description"),
            DisplayName = GetAttributeStringArg(prop, "XafDisplayName"),
            ToolTip = GetAttributeStringArg(prop, "ToolTip"),
            EditorAlias = GetAttributeStringArg(prop, "EditorAlias"),
            PersistentAlias = GetAttributeStringArg(prop, "PersistentAlias"),
            DataSourceCriteria = GetAttributeStringArg(prop, "DataSourceCriteria"),
            IsCollection = IsCollectionType(typeName),
            IsComputed = HasAttribute(prop, "PersistentAlias")
                         || HasAttribute(prop, "NotMapped")
                         || IsGetterOnly(prop),
            IsRequired = HasAttribute(prop, "RuleRequiredField")
                         || HasAttribute(prop, "Required"),
            ImmediatePostData = HasAttribute(prop, "ImmediatePostData"),
            IsKey = HasAttribute(prop, "Key"),
            // [Indexed] alone is a performance hint; [Indexed(Unique = true)] is a rule the
            // database enforces, and the two are the same attribute.
            IsUnique = GetAttributeStringArg(prop, "Indexed", "Unique") is { } unique
                       && unique.Equals("true", StringComparison.OrdinalIgnoreCase),
        };

        // Size attribute (XPO)
        var sizeValue = GetAttributeStringArg(prop, "Size");
        if (int.TryParse(sizeValue, out int size))
            extracted.Size = size;

        // StringLength / MaxLength attributes (EF Core)
        if (!extracted.Size.HasValue)
        {
            var stringLengthValue = GetAttributeStringArg(prop, "StringLength")
                                    ?? GetAttributeStringArg(prop, "MaxLength");
            if (int.TryParse(stringLengthValue, out int stringLength))
                extracted.Size = stringLength;
        }

        // VisibleInListView / VisibleInDetailView
        var listVisible = GetAttributeStringArg(prop, "VisibleInListView");
        if (listVisible != null)
            extracted.VisibleInListView = listVisible != "false" && listVisible != "False";

        var detailVisible = GetAttributeStringArg(prop, "VisibleInDetailView");
        if (detailVisible != null)
            extracted.VisibleInDetailView = detailVisible != "false" && detailVisible != "False";

        // DisplayFormat from ModelDefault
        extracted.DisplayFormat = GetModelDefaultValue(prop, "DisplayFormat");

        // Default value from ModelDefault or initializer
        extracted.DefaultValue = GetModelDefaultValue(prop, "DefaultValue")
                                 ?? GetPropertyInitializer(prop);

        // Collect custom attributes for completeness
        foreach (var attrList in prop.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if (!IsCommonAttribute(attrName))
                {
                    extracted.CustomAttributes.Add(attr.ToString());
                }
            }
        }

        // Validation rules on properties
        foreach (var attrList in prop.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if (attrName.StartsWith("Rule"))
                {
                    extracted.IsRequired = extracted.IsRequired || attrName == "RuleRequiredField";
                }
            }
        }

        return extracted;
    }

    /// <summary>
    /// Extracts association metadata from a property when relationship attributes are present.
    /// </summary>
    private static ExtractedRelationship? ExtractRelationship(PropertyDeclarationSyntax prop)
    {
        var associationName = GetAttributeStringArg(prop, "Association");
        if (associationName == null) return null;

        var typeName = prop.Type.ToString();
        var isCollection = IsCollectionType(typeName);
        var isAggregated = HasAttribute(prop, "Aggregated");

        string relatedEntity;
        RelationshipType relType;

        if (isCollection)
        {
            // Extract generic type argument: XPCollection<Factura> -> Factura
            relatedEntity = ExtractGenericArgument(typeName);
            relType = RelationshipType.OneToMany;
        }
        else
        {
            relatedEntity = typeName;
            relType = RelationshipType.ManyToOne;
        }

        return new ExtractedRelationship
        {
            PropertyName = prop.Identifier.Text,
            RelatedEntity = relatedEntity,
            AssociationName = associationName,
            Type = relType,
            IsAggregated = isAggregated
        };
    }

    /// <summary>
    /// Post-extraction pass: infer relationships from EF Core navigation properties
    /// that don't have an explicit [Association] attribute.
    /// </summary>
    private static void InferEfCoreRelationships(List<ExtractedEntity> entities)
    {
        var entityNames = new HashSet<string>(entities.Select(e => e.ClassName));

        foreach (var entity in entities)
        {
            foreach (var prop in entity.Properties)
            {
                // Skip if already has a relationship for this property
                if (entity.Relationships.Any(r => r.PropertyName == prop.Name))
                    continue;

                if (prop.IsCollection)
                {
                    // Collection nav property -> OneToMany
                    var genericArg = ExtractGenericArgument(prop.TypeName);
                    if (entityNames.Contains(genericArg))
                    {
                        entity.Relationships.Add(new ExtractedRelationship
                        {
                            PropertyName = prop.Name,
                            RelatedEntity = genericArg,
                            Type = RelationshipType.OneToMany,
                            IsAggregated = prop.CustomAttributes.Any(a => a.Contains("Aggregated"))
                        });
                    }
                }
                else if (entityNames.Contains(prop.TypeName))
                {
                    // Reference nav property -> ManyToOne
                    entity.Relationships.Add(new ExtractedRelationship
                    {
                        PropertyName = prop.Name,
                        RelatedEntity = prop.TypeName,
                        Type = RelationshipType.ManyToOne,
                        IsAggregated = false
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extracts class-level and property-level validation rules.
    /// </summary>
    private static List<ExtractedValidationRule> ExtractValidationRules(ClassDeclarationSyntax classDecl)
    {
        var rules = new List<ExtractedValidationRule>();

        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (!name.StartsWith("Rule")) continue;

                var rule = new ExtractedValidationRule
                {
                    RuleType = name,
                };

                ApplyRuleArguments(rule, attr);
                rules.Add(rule);
            }
        }

        // Also check property-level validation rules
        foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            foreach (var attrList in prop.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (!name.StartsWith("Rule")) continue;

                    var rule = new ExtractedValidationRule
                    {
                        RuleType = name,
                        TargetProperty = prop.Identifier.Text,
                    };

                    ApplyRuleArguments(rule, attr);
                    rules.Add(rule);
                }
            }
        }

        return rules;
    }

    /// <summary>
    /// Reads a validation attribute's arguments into a rule.
    /// </summary>
    /// <remarks>
    /// Shared by the class-level and property-level paths, which had drifted apart: the
    /// property-level one recorded arguments into <see cref="ExtractedValidationRule.Parameters"/>
    /// but never set <see cref="ExtractedValidationRule.MessageTemplate"/>. Property-level rules
    /// are the ordinary way to write XAF validation, and the message is the most useful part of a
    /// rule — it is what the user is told when the rule fires — so it was missing from exactly the
    /// rules people write most.
    /// <para>
    /// Positional arguments are recorded as <c>arg0</c>, <c>arg1</c>, … rather than under one
    /// shared key, which previously let each overwrite the last.
    /// </para>
    /// </remarks>
    private static void ApplyRuleArguments(ExtractedValidationRule rule, AttributeSyntax attr)
    {
        if (attr.ArgumentList is null)
            return;

        var args = attr.ArgumentList.Arguments;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            var argName = arg.NameEquals?.Name.ToString() ?? $"arg{index}";
            var argValue = SyntaxLiteral.ValueOf(arg.Expression);

            rule.Parameters[argName] = argValue;

            if (argName.Contains("Message", StringComparison.OrdinalIgnoreCase))
                rule.MessageTemplate = argValue;
            else if (argName.Equals("TargetCriteria", StringComparison.OrdinalIgnoreCase))
                rule.TargetCriteria = argValue;
            else if (argName.Equals("Criteria", StringComparison.OrdinalIgnoreCase))
                rule.Expression = argValue;
            else if (argName.Equals("TargetPropertyName", StringComparison.OrdinalIgnoreCase))
                rule.TargetProperty = argValue;
        }

        rule.Expression ??= PositionalCriteria(rule, args);
    }

    /// <summary>
    /// The expression a <c>RuleCriteria</c> enforces, when it was passed positionally.
    /// </summary>
    /// <remarks>
    /// Its overloads put the criteria in different positions — <c>("Total &gt;= 0")</c>,
    /// <c>("id", DefaultContexts.Save, "Total &gt;= 0")</c> — so the position cannot be hardcoded.
    /// The last positional string <em>literal</em> is the criteria in every overload: the id comes
    /// before it, and the contexts argument is an enum in the form people write.
    /// </remarks>
    private static string? PositionalCriteria(ExtractedValidationRule rule, SeparatedSyntaxList<AttributeArgumentSyntax> args)
    {
        if (!rule.RuleType.Contains("Criteria", StringComparison.Ordinal))
            return null;

        string? found = null;

        foreach (var arg in args)
        {
            if (arg.NameEquals is not null)
                break;

            if (arg.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                found = literal.Token.ValueText;
        }

        return found;
    }

    /// <summary>
    /// Extracts appearance rules from class attributes.
    /// </summary>
    private static List<ExtractedAppearanceRule> ExtractAppearanceRules(ClassDeclarationSyntax classDecl)
    {
        var rules = new List<ExtractedAppearanceRule>();

        var allAttributes = classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => a.Name.ToString().Contains("Appearance"));

        foreach (var attr in allAttributes)
        {
            var rule = new ExtractedAppearanceRule();
            if (attr.ArgumentList == null) continue;

            var args = attr.ArgumentList.Arguments.ToList();

            // First positional argument is typically the ID
            if (args.Count > 0)
                rule.Id = SyntaxLiteral.ValueOf(args[0].Expression);

            foreach (var arg in args)
            {
                var name = arg.NameEquals?.Name.ToString();
                var value = SyntaxLiteral.ValueOf(arg.Expression);

                switch (name)
                {
                    case "TargetItems": rule.TargetItems = value; break;
                    case "Criteria": rule.Criteria = value; break;
                    case "Context": rule.Context = value; break;
                    case "Visibility": rule.Visibility = value; break;
                    case "Enabled": rule.Enabled = value; break;
                    case "BackColor": rule.BackColor = value; break;
                    case "FontColor": rule.FontColor = value; break;
                }
            }

            rules.Add(rule);
        }

        return rules;
    }

    #region Helper Methods

    /// <summary>
    /// Detects ORM mode by scanning file contents for EF-specific namespaces.
    /// </summary>
    /// <summary>
    /// Decides which ORM an application persists with, from what its source declares.
    /// </summary>
    /// <remarks>
    /// Read as syntax rather than as text. Scanning file contents for a namespace counts a mention
    /// of it in a comment, a string or an <c>#if</c>-disabled block as evidence — and a project
    /// that merely discusses EF Core is not one that uses it.
    /// <para>
    /// The signals are ranked by what each one costs to be wrong about. A <c>DbSet&lt;T&gt;</c>
    /// registered on a context is the application declaring a table, and it cannot be mistaken;
    /// a <c>using</c> directive is weaker but still deliberate. Where neither ORM leaves any
    /// trace the answer is <see cref="OrmType.Unknown"/>, because the alternative is to state a
    /// default in the same voice as everything that was actually read.
    /// </para>
    /// </remarks>
    private static OrmType DetectOrmType(IEnumerable<SyntaxNode> roots, DbSetRoster roster)
    {
        // The application cannot run without its registrations being right, which makes them the
        // one signal that is never incidental.
        if (roster.RegistersAnything)
            return OrmType.EfCore;

        var efCore = false;
        var xpo = false;

        foreach (var root in roots)
        {
            foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                if (directive.Alias is not null || directive.Name is null)
                    continue;

                var name = directive.Name.ToString();

                // Exact or namespace-prefixed, never StartsWith on its own:
                // `DevExpress.Persistent.BaseImpl` is XPO and a prefix of the EF Core one.
                if (IsOrDescends(name, "Microsoft.EntityFrameworkCore")
                    || IsOrDescends(name, "DevExpress.Persistent.BaseImpl.EF")
                    || IsOrDescends(name, "DevExpress.ExpressApp.EFCore"))
                    efCore = true;
                else if (IsOrDescends(name, "DevExpress.Xpo"))
                    xpo = true;
            }

            // A base class is as deliberate as a using directive and survives file-scoped
            // namespaces that name nothing.
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                foreach (var baseTypeName in GetBaseTypeNames(classDecl))
                {
                    if (baseTypeName is "XPObject" or "XPCustomObject" or "XPLiteObject" or "XPBaseObject")
                        xpo = true;
                    else if (baseTypeName is "DbContext")
                        efCore = true;
                }
            }
        }

        if (efCore) return OrmType.EfCore;
        if (xpo) return OrmType.Xpo;

        return OrmType.Unknown;
    }

    private static bool IsOrDescends(string name, string ns) =>
        name.Equals(ns, StringComparison.Ordinal) || name.StartsWith($"{ns}.", StringComparison.Ordinal);

    /// <summary>
    /// The types an application registers as <c>DbSet&lt;T&gt;</c> on a <c>DbContext</c>, and the
    /// namespaces each registration could have been naming.
    /// </summary>
    /// <remarks>
    /// Under EF Core this is the application's own statement of what it persists, and it has to be
    /// right for the application to run at all — which makes it a better signal than a base class.
    /// An XAF project mapped onto an existing schema routinely has no XAF base class to match: the
    /// tables bring their own keys, so the project writes its own base, or maps a plain POCO.
    /// <para>
    /// Only classes declared in the analyzed source become entities. A DbContext also registers
    /// framework tables (<c>ModuleInfo</c>, <c>FileData</c>, <c>ModelDifference</c>) whose types
    /// are declared in DevExpress assemblies and are therefore never seen here, so they drop out
    /// without needing a list of names to exclude.
    /// </para>
    /// <para>
    /// The roster carries a namespace scope rather than a bare name because a bare name is not an
    /// identity: an application is free to have a <c>Contracts.Invoice</c> DTO beside its
    /// <c>BusinessObjects.Invoice</c> entity, and telling an agent the DTO is persistent is worse
    /// than the gap this roster exists to close. What is modelled here is ordinary C# lookup, the
    /// part of it syntax can see: the registering file's usings, its own namespace, and the
    /// namespaces enclosing it. Aliases, <c>using static</c> and extern aliases are not modelled —
    /// a registration that needs one of those finds no class and is dropped, which is the safe
    /// direction.
    /// </para>
    /// </remarks>
    private sealed class DbSetRoster
    {
        private readonly List<(string Argument, HashSet<string> Scopes)> _registrations = [];

        /// <summary>Whether any context in the source registers anything at all.</summary>
        public bool RegistersAnything => _registrations.Count > 0;

        /// <summary>
        /// Reads every <c>DbSet&lt;T&gt;</c> property declared on a context in the parsed source.
        /// </summary>
        public static DbSetRoster Read(IEnumerable<SyntaxNode> roots)
        {
            var roster = new DbSetRoster();
            var trees = roots.ToList();

            // `global using` reaches every file, so it has to be gathered before any one file is
            // read -- including from a GlobalUsings.cs that declares nothing else.
            var globalUsings = trees
                .SelectMany(root => root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                .Where(directive => !directive.GlobalKeyword.IsKind(SyntaxKind.None))
                .Select(directive => directive.Name?.ToString())
                .Where(name => name is not null)
                .Select(name => name!)
                .ToHashSet(StringComparer.Ordinal);

            var contexts = FindContextClasses(trees);

            foreach (var context in contexts)
            {
                var root = context.SyntaxTree.GetRoot();
                var scopes = new HashSet<string>(globalUsings, StringComparer.Ordinal);

                foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    if (directive.Alias is null && directive.Name is not null)
                        scopes.Add(directive.Name.ToString());
                }

                // The context's own namespace, and every namespace enclosing it: C# resolves an
                // unqualified name outwards, so an entity one level up needs no using directive.
                var contextNamespace = GetNamespace(context);
                for (var scope = contextNamespace; ; )
                {
                    scopes.Add(scope);
                    var dot = scope.LastIndexOf('.');
                    if (dot < 0) break;
                    scope = scope[..dot];
                }

                foreach (var property in context.Members.OfType<PropertyDeclarationSyntax>())
                {
                    if (property.Type is not GenericNameSyntax generic) continue;
                    if (generic.Identifier.Text != "DbSet") continue;
                    if (generic.TypeArgumentList.Arguments.Count != 1) continue;

                    var argument = generic.TypeArgumentList.Arguments[0].ToString();
                    if (argument.Length > 0)
                        roster._registrations.Add((argument, scopes));
                }
            }

            return roster;
        }

        /// <summary>
        /// Whether the application registers this class -- by name, and from somewhere that could
        /// actually have been naming this one.
        /// </summary>
        public bool Registers(string @namespace, string className)
        {
            foreach (var (argument, scopes) in _registrations)
            {
                var dot = argument.LastIndexOf('.');
                var simpleName = dot < 0 ? argument : argument[(dot + 1)..];
                if (!string.Equals(simpleName, className, StringComparison.Ordinal)) continue;

                if (dot < 0)
                {
                    if (scopes.Contains(@namespace)) return true;
                    continue;
                }

                // A qualified registration -- DbSet<Contracts.Invoice> -- names its own namespace
                // tail, and that is more specific than anything the usings could tell us.
                var qualifier = argument[..dot];
                if (@namespace.Equals(qualifier, StringComparison.Ordinal)
                    || @namespace.EndsWith("." + qualifier, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The classes that are a <c>DbContext</c>, following base classes declared in the source.
        /// </summary>
        /// <remarks>
        /// Following the chain matters because an application that writes its own
        /// <c>AuditedDbContext : DbContext</c> and derives every real context from it would
        /// otherwise register nothing -- the same silent-empty failure this roster is here to fix.
        /// <para>
        /// The chain is walked by simple name, which cannot distinguish two same-named classes in
        /// different namespaces. The cost of being wrong is small in this direction: a class only
        /// contributes to the roster if it also declares <c>DbSet&lt;T&gt;</c> properties, which
        /// something that is not a context does not do.
        /// </para>
        /// </remarks>
        private static List<ClassDeclarationSyntax> FindContextClasses(List<SyntaxNode> trees)
        {
            var declared = trees
                .SelectMany(root => root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                .ToList();

            var contextNames = new HashSet<string>(StringComparer.Ordinal) { "DbContext" };

            // A fixed point, because a base class may be read after the class deriving from it.
            bool grew;
            do
            {
                grew = false;
                foreach (var candidate in declared)
                {
                    if (contextNames.Contains(candidate.Identifier.Text)) continue;
                    if (!GetBaseTypeNames(candidate).Any(contextNames.Contains)) continue;

                    contextNames.Add(candidate.Identifier.Text);
                    grew = true;
                }
            } while (grew);

            return declared
                .Where(candidate => GetBaseTypeNames(candidate).Any(contextNames.Contains))
                .ToList();
        }
    }

    /// <summary>
    /// Folds the parts of a <c>partial</c> class into the one entity they describe.
    /// </summary>
    /// <remarks>
    /// A class matched by its base list could only ever match once, because only one part declares
    /// the base list. A class matched by the DbSet roster matches on its name, so every part
    /// matches -- and a scaffolded legacy schema, which is exactly what the roster is for, splits
    /// its classes as a matter of routine. Left alone that reports the entity twice, each copy
    /// holding half its properties: two incomplete truths, and no way for a reader to tell they
    /// are the same class.
    /// <para>
    /// Merging also recovers the members XPO extraction has always dropped, where a hand-written
    /// part carries <c>: BaseObject</c> and a generated part carries half the columns.
    /// </para>
    /// </remarks>
    private static List<ExtractedEntity> MergePartialDeclarations(List<ExtractedEntity> entities)
    {
        var parts = new Dictionary<(string Namespace, string ClassName), List<ExtractedEntity>>();
        var order = new List<(string Namespace, string ClassName)>();

        foreach (var entity in entities)
        {
            var key = (entity.Namespace, entity.ClassName);
            if (!parts.TryGetValue(key, out var group))
            {
                parts[key] = group = [];
                order.Add(key);
            }
            group.Add(entity);
        }

        var merged = new List<ExtractedEntity>();

        foreach (var key in order)
        {
            var group = parts[key];

            // The part declaring the base list is the hand-written one, and it is where the class
            // attributes live. Taking whichever half came first instead would make the reported
            // file, and the order of the columns, depend on the file system doing the listing.
            var primary = group.Find(part => part.BaseTypes.Count > 0) ?? group[0];
            merged.Add(primary);

            foreach (var entity in group)
            {
                if (ReferenceEquals(entity, primary)) continue;

                primary.Description ??= entity.Description;
                primary.NavigationGroup ??= entity.NavigationGroup;
                primary.DefaultProperty ??= entity.DefaultProperty;
                primary.ModelCaption ??= entity.ModelCaption;
                primary.SourceProject ??= entity.SourceProject;
                primary.IsDefaultClassOptions |= entity.IsDefaultClassOptions;
                primary.IsCloneable |= entity.IsCloneable;

                // Non-persistent anywhere means non-persistent: one part saying so is the class.
                primary.IsPersistent &= entity.IsPersistent;

                if (primary.BaseType is "object" or "" && entity.BaseType is not ("object" or ""))
                    primary.BaseType = entity.BaseType;

                foreach (var baseType in entity.BaseTypes.Where(name => !primary.BaseTypes.Contains(name)))
                    primary.BaseTypes.Add(baseType);

                var known = primary.Properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
                primary.Properties.AddRange(entity.Properties.Where(property => known.Add(property.Name)));

                primary.Relationships.AddRange(entity.Relationships);
                primary.ValidationRules.AddRange(entity.ValidationRules);
                primary.AppearanceRules.AddRange(entity.AppearanceRules);
                primary.InferredBusinessRules.AddRange(entity.InferredBusinessRules);
                primary.SourceComments.AddRange(entity.SourceComments);
            }
        }

        return merged;
    }

    private static bool IsXafBusinessObject(ClassDeclarationSyntax classDecl, string[] baseTypeNames)
    {
        if (classDecl.BaseList == null) return false;

        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();
            // Check direct match or generic base (e.g., ViewController<T>)
            var simpleTypeName = typeName.Contains('<') ? typeName[..typeName.IndexOf('<')] : typeName;

            if (baseTypeNames.Any(bt => simpleTypeName.Equals(bt, StringComparison.Ordinal)
                                        || simpleTypeName.EndsWith($".{bt}")))
                return true;
        }

        return false;
    }

    private static bool IsCollectionType(string typeName)
    {
        return typeName.StartsWith("XPCollection")
               || typeName.Contains("IList")
               || typeName.Contains("ICollection")
               || typeName.StartsWith("ObservableCollection")
               || typeName.StartsWith("List<");
    }

    private static string GetNamespace(ClassDeclarationSyntax classDecl)
    {
        var nsDecl = classDecl.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return nsDecl?.Name.ToString() ?? string.Empty;
    }

    private static string GetBaseTypeName(ClassDeclarationSyntax classDecl)
    {
        return classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString() ?? "object";
    }

    /// <summary>
    /// Every name in the class's base list, with namespaces and generic arguments removed.
    /// </summary>
    private static List<string> GetBaseTypeNames(ClassDeclarationSyntax classDecl)
    {
        var names = new List<string>();

        foreach (var baseType in classDecl.BaseList?.Types ?? default)
        {
            var name = baseType.Type.ToString();
            var generic = name.IndexOf('<');

            if (generic > 0)
                name = name[..generic];

            var lastDot = name.LastIndexOf('.');

            if (lastDot >= 0)
                name = name[(lastDot + 1)..];

            if (name.Length > 0 && !names.Contains(name, StringComparer.Ordinal))
                names.Add(name);
        }

        return names;
    }

    private static bool HasAttribute(MemberDeclarationSyntax member, string attributeName)
    {
        return member.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => MatchesAttributeName(a.Name.ToString(), attributeName));
    }

    private static string? GetAttributeStringArg(MemberDeclarationSyntax member, string attributeName)
    {
        var attr = member.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => MatchesAttributeName(a.Name.ToString(), attributeName));

        if (attr?.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
            return null;

        return SyntaxLiteral.ValueOf(attr.ArgumentList.Arguments[0].Expression);
    }

    /// <summary>
    /// Reads one named argument of an attribute, e.g. the <c>Unique</c> of <c>[Indexed]</c>.
    /// </summary>
    private static string? GetAttributeStringArg(
        MemberDeclarationSyntax member,
        string attributeName,
        string argumentName)
    {
        var attr = member.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => MatchesAttributeName(a.Name.ToString(), attributeName));

        var argument = attr?.ArgumentList?.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.ToString() == argumentName);

        return argument is null ? null : SyntaxLiteral.ValueOf(argument.Expression);
    }

    private static string? GetModelDefaultValue(PropertyDeclarationSyntax prop, string propertyName)
    {
        var attrs = prop.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => MatchesAttributeName(a.Name.ToString(), "ModelDefault"));

        foreach (var attr in attrs)
        {
            if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count < 2) continue;

            var firstArg = SyntaxLiteral.ValueOf(attr.ArgumentList.Arguments[0].Expression);
            if (firstArg.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return SyntaxLiteral.ValueOf(attr.ArgumentList.Arguments[1].Expression);
            }
        }

        return null;
    }

    private static string? GetPropertyInitializer(PropertyDeclarationSyntax prop)
    {
        return prop.Initializer?.Value.ToString();
    }

    private static bool IsGetterOnly(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList == null) return prop.ExpressionBody != null;
        return !prop.AccessorList.Accessors.Any(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
    }

    private static bool MatchesAttributeName(string fullName, string shortName)
    {
        var normalized = fullName.Replace("Attribute", "");
        return normalized.Equals(shortName, StringComparison.Ordinal)
               || normalized.EndsWith($".{shortName}");
    }

    private static string ExtractGenericArgument(string typeName)
    {
        var start = typeName.IndexOf('<');
        var end = typeName.LastIndexOf('>');
        if (start >= 0 && end > start)
            return typeName[(start + 1)..end].Trim();
        return typeName;
    }

    private static bool IsInfrastructureProperty(string propertyName)
    {
        return propertyName is
            // XPO infrastructure
            "Session" or "ClassInfo" or "This" or "Loading"
            or "IsLoading" or "IsDeleted" or "IsSaving" or "Oid"
            or "GCRecord" or "OptimisticLockField"
            // EF Core infrastructure
            or "ObjectSpace" or "ID";
    }

    private static bool IsCommonAttribute(string attrName)
    {
        return attrName is "Description" or "NavigationItem" or "XafDefaultProperty"
            or "DefaultProperty" or "DefaultClassOptions" or "Association"
            or "Aggregated" or "Size" or "VisibleInListView" or "VisibleInDetailView"
            or "XafDisplayName" or "ToolTip" or "EditorAlias" or "PersistentAlias"
            or "DataSourceCriteria" or "ImmediatePostData" or "Key" or "ModelDefault"
            or "NonPersistent" or "DomainComponent" or "RuleRequiredField"
            // EF Core / DataAnnotations attributes
            or "Required" or "StringLength" or "MaxLength" or "NotMapped"
            or "ForeignKey" or "Column" or "Table" or "InverseProperty";
    }

    private static List<string> ExtractComments(ClassDeclarationSyntax classDecl)
    {
        var comments = new List<string>();

        var trivia = classDecl.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
                        || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        foreach (var t in trivia)
        {
            comments.Add(t.ToString().Trim());
        }

        return comments;
    }

    /// <summary>
    /// Locates C# files from configured patterns, applying directory exclusions.
    /// </summary>
    private static IEnumerable<string> FindFiles(string sourceDirectory, string[] patterns, string[] excludePatterns)
    {
        var allFiles = new HashSet<string>();

        foreach (var pattern in patterns)
        {
            // Convert glob pattern to directory search
            var dir = sourceDirectory;
            var searchPattern = "*.cs";

            if (pattern.Contains("BusinessObjects"))
            {
                var boDir = Path.Combine(sourceDirectory, "BusinessObjects");
                if (Directory.Exists(boDir))
                    dir = boDir;
            }
            else if (pattern.Contains("Controllers"))
            {
                var ctrlDir = Path.Combine(sourceDirectory, "Controllers");
                if (Directory.Exists(ctrlDir))
                    dir = ctrlDir;
            }

            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories))
                {
                    // NOTE: the configured glob-like exclude patterns are still not evaluated;
                    // only build output is filtered. Applying them properly is tracked separately.
                    if (BuildOutputFilter.IsAnalyzable(file, dir))
                        allFiles.Add(file);
                }
            }
        }

        return allFiles;
    }

    #endregion
}
