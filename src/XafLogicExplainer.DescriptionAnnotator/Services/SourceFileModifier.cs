using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XafLogicExplainer.DescriptionAnnotator.Services;

/// <summary>
/// Applies generated description suggestions back into C# source files using Roslyn rewrites.
/// </summary>
public class SourceFileModifier
{
    /// <summary>
    /// Apply [Description] attributes to source files based on generated descriptions.
    /// Returns the list of files modified.
    /// </summary>
    public List<string> Apply(List<MissingDescriptionItem> items)
    {
        var modifiedFiles = new HashSet<string>();

        // Group by file
        var grouped = items
            .Where(i => !string.IsNullOrEmpty(i.SuggestedDescription) && !i.SuggestedDescription!.StartsWith("["))
            .GroupBy(i => i.FilePath);

        foreach (var group in grouped)
        {
            var filePath = group.Key;
            var source = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            // Ensure using System.ComponentModel is present
            var hasUsing = root.Usings.Any(u => u.Name?.ToString() == "System.ComponentModel");

            var rewriter = new DescriptionRewriter(group.ToList());
            var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

            if (rewriter.ModificationsApplied > 0)
            {
                // Add using if needed
                if (!hasUsing)
                {
                    var usingDirective = SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName("System.ComponentModel"))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    newRoot = newRoot.AddUsings(usingDirective);
                }

                File.WriteAllText(filePath, newRoot.ToFullString());
                modifiedFiles.Add(filePath);
            }
        }

        return modifiedFiles.ToList();
    }

    /// <summary>
    /// Generate a Markdown report of suggested descriptions.
    /// </summary>
    public static string GenerateReport(ScanResult scanResult, List<MissingDescriptionItem> itemsWithSuggestions)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Description Annotator Report");
        sb.AppendLine();
        sb.AppendLine($"**Project:** {scanResult.ProjectPath}");
        sb.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Count |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| Total classes scanned | {scanResult.TotalClasses} |");
        sb.AppendLine($"| Total properties scanned | {scanResult.TotalProperties} |");
        sb.AppendLine($"| Classes missing [Description] | {scanResult.MissingClassDescriptions} |");
        sb.AppendLine($"| Properties missing [Description] | {scanResult.MissingPropertyDescriptions} |");
        sb.AppendLine();

        // Group by file
        var grouped = itemsWithSuggestions.GroupBy(i => i.ClassName);

        sb.AppendLine("## Suggested Descriptions");
        sb.AppendLine();

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            sb.AppendLine($"### {group.Key}");
            sb.AppendLine();

            var classItem = group.FirstOrDefault(i => i.MemberType == "class");
            if (classItem != null)
            {
                sb.AppendLine($"**Class:** `[Description(\"{classItem.SuggestedDescription}\")]`");
                sb.AppendLine();
            }

            var props = group.Where(i => i.MemberType == "property").ToList();
            if (props.Count > 0)
            {
                sb.AppendLine("| Property | Type | Suggested Description |");
                sb.AppendLine("|----------|------|----------------------|");
                foreach (var prop in props)
                {
                    sb.AppendLine($"| `{prop.MemberName}` | `{prop.TypeName}` | {prop.SuggestedDescription} |");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Roslyn syntax rewriter that inserts [Description("...")] attributes.
    /// </summary>
    private class DescriptionRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<(string className, string? memberName), string> _descriptions;

        /// <summary>
        /// Number of attribute insertions applied by this rewrite instance.
        /// </summary>
        public int ModificationsApplied { get; private set; }

        /// <summary>
        /// Creates a syntax rewriter from generated suggestion items.
        /// </summary>
        public DescriptionRewriter(List<MissingDescriptionItem> items)
        {
            _descriptions = items
                .Where(i => !string.IsNullOrEmpty(i.SuggestedDescription))
                .ToDictionary(
                    i => (i.ClassName, i.MemberName),
                    i => i.SuggestedDescription!);
        }

        /// <summary>
        /// Adds class-level description attributes when available.
        /// </summary>
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var key = (node.Identifier.Text, (string?)null);
            if (_descriptions.TryGetValue(key, out var desc))
            {
                node = AddDescriptionAttribute(node, desc);
                ModificationsApplied++;
            }

            return base.VisitClassDeclaration(node);
        }

        /// <summary>
        /// Adds property-level description attributes when available.
        /// </summary>
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null) return base.VisitPropertyDeclaration(node);

            var key = (classDecl.Identifier.Text, (string?)node.Identifier.Text);
            if (_descriptions.TryGetValue(key, out var desc))
            {
                node = AddDescriptionAttribute(node, desc);
                ModificationsApplied++;
            }

            return base.VisitPropertyDeclaration(node);
        }

        /// <summary>
        /// Creates and appends a <c>[Description("...")]</c> attribute node.
        /// </summary>
        private static T AddDescriptionAttribute<T>(T node, string description) where T : MemberDeclarationSyntax
        {
            // Escape quotes in description
            var escapedDesc = description.Replace("\"", "\\\"");

            var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("Description"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(escapedDesc))))));

            var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(attribute))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            // Get existing leading trivia (whitespace/indentation)
            var leadingTrivia = node.GetLeadingTrivia();
            var indentation = leadingTrivia.Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).LastOrDefault();

            if (indentation != default)
                attributeList = attributeList.WithLeadingTrivia(indentation);

            return (T)node.AddAttributeLists(attributeList);
        }
    }
}
