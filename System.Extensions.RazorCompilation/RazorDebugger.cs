#if NETCORE
namespace System.Extensions.RazorCompilation
{
    using System.IO;
    using System.Diagnostics;
    using System.Reflection;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Razor.Language;
    using Microsoft.AspNetCore.Razor.Language.Extensions;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using System.Runtime.Loader;
    public static class RazorDebugger
    {
        public static List<KeyValuePair<string, Func<object>>> Execute(object debugObj)
        {
            if (debugObj == null)
                return null;

            var @namespace = (string)debugObj.GetType().GetProperty("Namespace").GetValue(debugObj);
            var baseType = (string)debugObj.GetType().GetProperty("BaseType").GetValue(debugObj);
            var razorPath = (string)debugObj.GetType().GetProperty("RazorPath").GetValue(debugObj);

            var cSharpIdentifier = typeof(RazorProjectEngine).Assembly.GetType("Microsoft.AspNetCore.Razor.Language.CSharpIdentifier");
            var sanitizeIdentifier = cSharpIdentifier.GetMethod("SanitizeIdentifier", BindingFlags.Public | BindingFlags.Static);
            var engine = RazorProjectEngine.Create(
                    RazorConfiguration.Default,
                    RazorProjectFileSystem.Create(razorPath),
                    (builder) =>
                    {
                        SectionDirective.Register(builder);
                        builder.SetNamespace(@namespace)
                            .AddDefaultImports(@"
                            @using System
                            @using System.Collections.Generic
                            @using System.Threading.Tasks
                            @using Microsoft.AspNetCore.Mvc
                            ");
                        builder.ConfigureClass(
                            (document, @class) =>
                            {
                                @class.BaseType = baseType;
                                var relativePath = document.Source.RelativePath;
                                @class.ClassName = (string)sanitizeIdentifier.Invoke(null, new[] { relativePath.Substring(0, relativePath.Length - ".cshtml".Length) });
                            });
                    });

            var debugItems = new List<KeyValuePair<string, Func<object>>>();
            foreach (var projectItem in engine.FileSystem.EnumerateItems("/"))
            {
                debugItems.Add(new KeyValuePair<string, Func<object>>(
                    projectItem.FilePath, () => {
                        var code = engine.Process(projectItem).GetCSharpDocument().GeneratedCode;
                        var references = new List<MetadataReference>();
                        foreach (var reference in AssemblyLoadContext.Default.Assemblies)
                        {
                            if (reference.IsDynamic || string.IsNullOrEmpty(reference.Location) || !File.Exists(reference.Location))
                                continue;
                            references.Add(MetadataReference.CreateFromFile(reference.Location));
                        }
                        var syntaxTree = CSharpSyntaxTree.ParseText(code);
                        var compilation = CSharpCompilation.Create(
                            $"Razor_{Guid.NewGuid().ToString("N")}",
                            new[] { syntaxTree }, references,
                            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                        var assemblyStream = new MemoryStream();
                        var pdbStream = new MemoryStream();
                        var result = compilation.Emit(assemblyStream, pdbStream);
                        if (!result.Success)
                        {
                            string error = null;
                            foreach (var diagnostic in result.Diagnostics)
                            {
                                if (diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
                                {
                                    error += diagnostic.ToString();
                                }
                            }
                            throw new Exception(error);
                        }
                        assemblyStream.Seek(0, SeekOrigin.Begin);
                        pdbStream.Seek(0, SeekOrigin.Begin);
                        var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream, pdbStream);
                        return Activator.CreateInstance(assembly.GetTypes()[0]);
                    }));
            }
            return debugItems;
        }
    }
}
#endif