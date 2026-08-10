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

        // Resolve ORM type
        var ormType = options.Orm == OrmType.Auto
            ? DetectOrmType(csFiles)
            : options.Orm;
        options.ResolvedOrm = ormType;

        foreach (var file in csFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, path: file);
            var root = tree.GetRoot();

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classDeclarations)
            {
                if (IsXafBusinessObject(classDecl, options.BaseTypeNames))
                {
                    var entity = ExtractEntity(classDecl, file, options);
                    entities.Add(entity);
                }
            }
        }

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

                if (attr.ArgumentList != null)
                {
                    var args = attr.ArgumentList.Arguments.ToList();
                    foreach (var arg in args)
                    {
                        var argName = arg.NameEquals?.Name.ToString() ?? $"arg{args.IndexOf(arg)}";
                        var argValue = arg.Expression.ToString().Trim('"');
                        rule.Parameters[argName] = argValue;

                        if (argName.Equals("DefaultContexts", StringComparison.OrdinalIgnoreCase)
                            || argName.Equals("TargetCriteria", StringComparison.OrdinalIgnoreCase))
                            rule.TargetCriteria = argValue;

                        if (argName.Equals("MessageTemplateMustNotBeEmpty", StringComparison.OrdinalIgnoreCase)
                            || argName.Contains("Message"))
                            rule.MessageTemplate = argValue;

                        if (argName.Equals("TargetPropertyName", StringComparison.OrdinalIgnoreCase))
                            rule.TargetProperty = argValue;
                    }
                }

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

                    if (attr.ArgumentList != null)
                    {
                        foreach (var arg in attr.ArgumentList.Arguments)
                        {
                            var argName = arg.NameEquals?.Name.ToString() ?? "value";
                            rule.Parameters[argName] = arg.Expression.ToString().Trim('"');
                        }
                    }

                    rules.Add(rule);
                }
            }
        }

        return rules;
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
                rule.Id = args[0].Expression.ToString().Trim('"');

            foreach (var arg in args)
            {
                var name = arg.NameEquals?.Name.ToString();
                var value = arg.Expression.ToString().Trim('"');

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
    private static OrmType DetectOrmType(IEnumerable<string> csFiles)
    {
        foreach (var file in csFiles)
        {
            var source = File.ReadAllText(file);
            if (source.Contains("DevExpress.Persistent.BaseImpl.EF"))
                return OrmType.EfCore;
        }
        return OrmType.Xpo;
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

        return attr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
    }

    private static string? GetModelDefaultValue(PropertyDeclarationSyntax prop, string propertyName)
    {
        var attrs = prop.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => MatchesAttributeName(a.Name.ToString(), "ModelDefault"));

        foreach (var attr in attrs)
        {
            if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count < 2) continue;

            var firstArg = attr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
            if (firstArg.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return attr.ArgumentList.Arguments[1].Expression.ToString().Trim('"');
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
                    // HACK: Exclusion currently uses coarse obj/bin substring filtering and does not apply
                    // the configured glob-like exclude patterns. Kept for compatibility with existing behavior.
                    if (!excludePatterns.Any(ep => file.Contains("obj") || file.Contains("bin")))
                        allFiles.Add(file);
                }
            }
        }

        return allFiles;
    }

    #endregion
}
