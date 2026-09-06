using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynPad.Roslyn;
using Qx.Scripting;

namespace Qx.Ui;

public sealed class QxRoslynHost : RoslynHost
{
    public QxRoslynHost()
        : base(
            additionalAssemblies:
            [
                Assembly.Load("RoslynPad.Roslyn.Windows"),
                Assembly.Load("RoslynPad.Editor.Windows")
            ],
            references: RoslynHostReferences.NamespaceDefault.With(
                assemblyPathReferences: FrameworkReferencePaths(),
                assemblyReferences: ScriptEngine.ReferenceAssemblies,
                imports: ScriptEngine.Imports,
                typeNamespaceImports: [typeof(ScriptGlobals)]))
    {
    }

    private static IEnumerable<string> FrameworkReferencePaths()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string tpa)
            return [];

        return tpa.Split(Path.PathSeparator).Where(path =>
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("QX.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("RoslynPad", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("ICSharpCode", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("AvalonEdit", StringComparison.OrdinalIgnoreCase))
                return false;

            return name == "netstandard"
                || name == "mscorlib"
                || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                || name == "System"
                || name.StartsWith("Microsoft.CSharp", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft.VisualBasic", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft.Win32", StringComparison.OrdinalIgnoreCase);
        });
    }

    protected override Project CreateProject(Solution solution, DocumentCreationArgs args,
        CompilationOptions compilationOptions, Project? previousProject = null)
    {
        string name = args.Name ?? "Script";
        ProjectId id = ProjectId.CreateNewId(name);

        var parseOptions = new CSharpParseOptions(kind: SourceCodeKind.Script, languageVersion: LanguageVersion.Latest);

        if (compilationOptions is CSharpCompilationOptions csharp)
            compilationOptions = csharp.WithNullableContextOptions(NullableContextOptions.Disable);
        compilationOptions = compilationOptions.WithScriptClassName(name);

        solution = solution.AddProject(ProjectInfo.Create(
            id,
            VersionStamp.Create(),
            name,
            name,
            LanguageNames.CSharp,
            isSubmission: true,
            parseOptions: parseOptions,
            hostObjectType: typeof(ScriptGlobals),
            compilationOptions: compilationOptions,
            metadataReferences: previousProject is null ? DefaultReferences : ImmutableArray<MetadataReference>.Empty,
            projectReferences: previousProject is null ? null : [new ProjectReference(previousProject.Id)]));

        return solution.GetProject(id) ?? throw new InvalidOperationException("Failed to create Roslyn project.");
    }
}
