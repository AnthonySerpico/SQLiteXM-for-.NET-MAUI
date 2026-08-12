using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace QueryGalleryDemo.SourceGenerators;

/// <summary>
/// Incremental source generator that scans for <c>[QueryExample]</c>-attributed classes
/// implementing <c>IQueryExampleRunner</c>, extracts each <c>RunAsync</c> method body verbatim,
/// and emits a partial <c>GeneratedQueryExamples</c> class exposing the example metadata list
/// (with the extracted body as the <c>Code</c> string) and an id→factory runner dictionary.
///
/// The extracted body is what the user sees in the gallery UI — so display and execution are the
/// same source text by construction, making silent drift impossible.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QueryExampleGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "QueryGalleryDemo.Examples.QueryExampleAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ExampleModel?> models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => Transform(ctx, ct))
            .Where(static m => m is not null);

        IncrementalValueProvider<ImmutableArray<ExampleModel?>> collected = models.Collect();

        context.RegisterSourceOutput(collected, static (spc, examples) => Emit(spc, examples));
    }

    private static ExampleModel? Transform(GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol) return null;
        if (ctx.TargetNode is not ClassDeclarationSyntax classDecl) return null;
        if (ctx.Attributes.Length == 0) return null;

        AttributeData attr = ctx.Attributes[0];
        if (attr.ConstructorArguments.Length < 6) return null;

        string id          = attr.ConstructorArguments[0].Value as string ?? string.Empty;
        string name        = attr.ConstructorArguments[1].Value as string ?? string.Empty;
        string description = attr.ConstructorArguments[2].Value as string ?? string.Empty;
        int    category    = attr.ConstructorArguments[3].Value is int c ? c : 0;
        int    type        = attr.ConstructorArguments[4].Value is int t ? t : 0;
        string explanation = attr.ConstructorArguments[5].Value as string ?? string.Empty;

        // Locate RunAsync in this class.
        MethodDeclarationSyntax? runAsync = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == "RunAsync");
        if (runAsync?.Body is null) return null;

        string body = ExtractBodyText(runAsync.Body);

        return new ExampleModel(
            fullyQualifiedTypeName: classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            id: id,
            name: name,
            description: description,
            categoryValue: category,
            typeValue: type,
            explanation: explanation,
            codeBody: body);
    }

    /// <summary>
    /// Return the RunAsync method body with the outer braces removed and indentation normalized
    /// so the smallest-indented line starts at column 0. Preserves relative indentation and
    /// blank lines.
    /// </summary>
    private static string ExtractBodyText(BlockSyntax block)
    {
        // Grab the source between the opening '{' and closing '}' braces.
        SourceText srcText = block.SyntaxTree.GetText();
        int start = block.OpenBraceToken.Span.End;
        int end   = block.CloseBraceToken.Span.Start;
        string raw = srcText.GetSubText(TextSpan.FromBounds(start, end)).ToString();

        // Split into lines, drop leading/trailing purely-blank lines.
        string[] lines = raw.Replace("\r\n", "\n").Split('\n');

        int firstContent = 0;
        while (firstContent < lines.Length && string.IsNullOrWhiteSpace(lines[firstContent])) firstContent++;
        int lastContent = lines.Length - 1;
        while (lastContent >= firstContent && string.IsNullOrWhiteSpace(lines[lastContent])) lastContent--;
        if (firstContent > lastContent) return string.Empty;

        // Compute minimum leading whitespace across non-blank lines.
        int minIndent = int.MaxValue;
        for (int i = firstContent; i <= lastContent; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            int indent = 0;
            while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t')) indent++;
            if (indent < minIndent) minIndent = indent;
        }
        if (minIndent == int.MaxValue) minIndent = 0;

        var sb = new StringBuilder();
        for (int i = firstContent; i <= lastContent; i++)
        {
            string line = lines[i];
            if (line.Length >= minIndent)
                sb.Append(line, minIndent, line.Length - minIndent);
            else
                sb.Append(line.TrimStart());
            if (i < lastContent) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<ExampleModel?> examples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using QueryGalleryDemo.Examples;");
        sb.AppendLine("using QueryGalleryDemo.Models;");
        sb.AppendLine();
        sb.AppendLine("namespace QueryGalleryDemo.Examples.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static partial class GeneratedQueryExamples");
        sb.AppendLine("{");
        sb.AppendLine("    public static IReadOnlyList<QueryExample> All { get; } = BuildAll();");
        sb.AppendLine("    public static IReadOnlyDictionary<string, Func<IQueryExampleRunner>> Runners { get; } = BuildRunners();");
        sb.AppendLine();
        sb.AppendLine("    private static IReadOnlyList<QueryExample> BuildAll()");
        sb.AppendLine("    {");
        sb.AppendLine("        var list = new List<QueryExample>();");

        foreach (ExampleModel? m in examples)
        {
            if (m is null) continue;
            sb.AppendLine("        list.Add(new QueryExample");
            sb.AppendLine("        {");
            sb.Append("            Id = ");         AppendStringLiteral(sb, m.Id);         sb.AppendLine(",");
            sb.Append("            Name = ");       AppendStringLiteral(sb, m.Name);       sb.AppendLine(",");
            sb.Append("            Description = "); AppendStringLiteral(sb, m.Description); sb.AppendLine(",");
            sb.AppendLine($"            Category = (QueryGalleryDemo.Models.QueryCategory){m.CategoryValue},");
            sb.AppendLine($"            Type = (QueryGalleryDemo.Models.QueryType){m.TypeValue},");
            sb.Append("            Code = ");       AppendStringLiteral(sb, m.CodeBody);    sb.AppendLine(",");
            sb.Append("            Explanation = ");AppendStringLiteral(sb, m.Explanation); sb.AppendLine(",");
            sb.AppendLine("        });");
        }

        sb.AppendLine("        return list;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static IReadOnlyDictionary<string, Func<IQueryExampleRunner>> BuildRunners()");
        sb.AppendLine("    {");
        sb.AppendLine("        var map = new Dictionary<string, Func<IQueryExampleRunner>>(StringComparer.Ordinal);");
        foreach (ExampleModel? m in examples)
        {
            if (m is null) continue;
            sb.Append("        map[");
            AppendStringLiteral(sb, m.Id);
            sb.AppendLine($"] = () => new {m.FullyQualifiedTypeName}();");
        }
        sb.AppendLine("        return map;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("GeneratedQueryExamples.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void AppendStringLiteral(StringBuilder sb, string value)
    {
        // Emit as C# verbatim string with any embedded double quotes doubled.
        sb.Append("@\"");
        sb.Append(value.Replace("\"", "\"\""));
        sb.Append("\"");
    }

    private sealed class ExampleModel
    {
        public ExampleModel(
            string fullyQualifiedTypeName,
            string id,
            string name,
            string description,
            int categoryValue,
            int typeValue,
            string explanation,
            string codeBody)
        {
            FullyQualifiedTypeName = fullyQualifiedTypeName;
            Id = id;
            Name = name;
            Description = description;
            CategoryValue = categoryValue;
            TypeValue = typeValue;
            Explanation = explanation;
            CodeBody = codeBody;
        }

        public string FullyQualifiedTypeName { get; }
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int CategoryValue { get; }
        public int TypeValue { get; }
        public string Explanation { get; }
        public string CodeBody { get; }
    }
}
