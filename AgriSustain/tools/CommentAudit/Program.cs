using System.Security;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

// Maintainer tool: uses the SDK's Roslyn parser, never regexes C# declarations.
// Run from AgriSustain: dotnet run --project tools/CommentAudit -- check .
string mode = args.ElementAtOrDefault(0) ?? "check";
string root = Path.GetFullPath(args.ElementAtOrDefault(1) ?? ".");
var map = mode == "write" ? JsonSerializer.Deserialize<Dictionary<string, string>>(
    File.ReadAllText(args[2]))! : new Dictionary<string, string>();
var inventory = new List<object>();
var missing = new List<string>();
int documented = 0;
foreach (string directory in new[] { "src", "tests" })
{
    foreach (string file in Directory.GetFiles(Path.Combine(root, directory), "*.cs",
        SearchOption.AllDirectories).Where(file => !file.Split(Path.DirectorySeparatorChar)
            .Any(part => part is "obj" or "bin")))
    {
        var source = SourceText.From(File.ReadAllText(file));
        var tree = CSharpSyntaxTree.ParseText(source);
        var changes = new List<TextChange>();
        foreach (var node in tree.GetRoot().DescendantNodes().Where(node =>
            node is MethodDeclarationSyntax or ConstructorDeclarationSyntax
                or LocalFunctionStatementSyntax or ClassDeclarationSyntax { ParameterList: not null }))
        {
            string owner = node is ClassDeclarationSyntax declaration ? declaration.Identifier.Text
                : node.Ancestors().OfType<TypeDeclarationSyntax>().First().Identifier.Text;
            string name = node switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text,
                ConstructorDeclarationSyntax => ".ctor",
                ClassDeclarationSyntax => ".ctor",
                LocalFunctionStatementSyntax local => local.Identifier.Text,
                _ => throw new InvalidOperationException()
            };
            string key = owner + "." + name;
            bool exists = node.GetLeadingTrivia().Any(trivia =>
                trivia.GetStructure() is DocumentationCommentTriviaSyntax);
            inventory.Add(new { File = Path.GetRelativePath(root, file), Key = key, Exists = exists });
            if (exists)
            {
                documented++;
                continue;
            }

            if (mode != "write")
            {
                missing.Add(key);
                continue;
            }

            if (!map.TryGetValue(key, out string? description))
            {
                missing.Add(key);
                continue;
            }

            int start = source.Lines.GetLineFromPosition(node.SpanStart).Start;
            string indent = source.ToString(TextSpan.FromBounds(start, node.SpanStart));
            string documentation = indent + "/// <summary>\n";
            string line = indent + "///";
            foreach (string word in SecurityElement.Escape(description)!.Split(' '))
            {
                if (line.Length + word.Length + 1 > 98)
                {
                    documentation += line + "\n";
                    line = indent + "///";
                }
                line += " " + word;
            }
            documentation += line + "\n" + indent + "/// </summary>\n";
            changes.Add(new TextChange(new TextSpan(start, 0), documentation));
            documented++;
        }
        if (mode == "write" && changes.Count > 0)
            File.WriteAllText(file, source.WithChanges(changes).ToString());
    }
}
string output = Path.Combine(root, "..", "tmp", "function-comment-inventory.json");
File.WriteAllText(output, JsonSerializer.Serialize(inventory, new JsonSerializerOptions
    { WriteIndented = true }));
Console.WriteLine($"Documented: {documented}; missing: {missing.Count}; inventory: {output}");
if (mode == "check" || mode == "write")
{
    foreach (string key in missing.Distinct()) Console.WriteLine(key);
    if (missing.Count > 0) Environment.ExitCode = 1;
}
