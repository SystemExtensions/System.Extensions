
namespace System.Extensions.RazorCompilation
{
    using System;
    using System.IO;
    using System.Text;
    using System.Diagnostics;
    using System.Reflection;
    using System.Collections.Generic;
    using Microsoft.Build.Utilities;
    using Microsoft.Build.Framework;
    using Microsoft.AspNetCore.Razor.Language;
    using Microsoft.AspNetCore.Razor.Language.Extensions;
    public class RazorGenerator : Task
    {
        [Required]
        public string Configuration { get; set; }//TODO? DebugGenerate
        [Required]
        public string DebuggerPath { get; set; }
        [Required]
        public string Namespace { get; set; }
        [Required]
        public string BaseType { get; set; }
        [Required]
        public string RazorPath { get; set; }
        [Required]
        public string GeneratePath { get; set; }
        [Output]
        public ITaskItem[] GenerateFiles { get; set; }
        public override bool Execute()
        {
            if (!Directory.Exists(GeneratePath))
                Directory.CreateDirectory(GeneratePath);
            
            if (Configuration.Equals("DEBUG", StringComparison.OrdinalIgnoreCase))
            {
                var className = $"Debug_{Guid.NewGuid().ToString("N")}";
                var code = 
$@"using System;
using System.Runtime.Loader;

[assembly: global::Microsoft.AspNetCore.Razor.Hosting.RazorDebugAttribute(@""{Namespace}"", @""{BaseType}"", @""{RazorPath}"", typeof({Namespace}.{className}))]
namespace {Namespace}
{{
    public static class {className}
    {{
        public static object Execute(object debugObj)
        {{
            AssemblyLoadContext.Default.LoadFromAssemblyPath(@""{DebuggerPath}Microsoft.AspNetCore.Razor.Language.dll"");
            AssemblyLoadContext.Default.LoadFromAssemblyPath(@""{DebuggerPath}Microsoft.CodeAnalysis.dll"");
            AssemblyLoadContext.Default.LoadFromAssemblyPath(@""{DebuggerPath}Microsoft.CodeAnalysis.CSharp.dll"");
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(@""{DebuggerPath}System.Extensions.RazorCompilation.dll"");
            var debugger = assembly.GetType(""System.Extensions.RazorCompilation.RazorDebugger"");
            return debugger.GetMethod(""Execute"").Invoke(null, new[] {{ debugObj }});
        }}
    }}
}}";
                var generateFile = Path.Combine(GeneratePath, "Debug.cs");
                File.WriteAllText(generateFile, code, Encoding.UTF8);
                GenerateFiles = new[] { new TaskItem(generateFile) };
                return true;
            }
            else 
            {
                var cSharpIdentifier = typeof(RazorProjectEngine).Assembly.GetType("Microsoft.AspNetCore.Razor.Language.CSharpIdentifier");
                var sanitizeIdentifier = cSharpIdentifier.GetMethod("SanitizeIdentifier", BindingFlags.Public | BindingFlags.Static);
                var engine = RazorProjectEngine.Create(
                        RazorConfiguration.Default,
                        RazorProjectFileSystem.Create(RazorPath),
                        (builder) =>
                        {
                            SectionDirective.Register(builder);
                            builder.SetNamespace(Namespace)
                                .AddDefaultImports(@"
                            @using System
                            @using System.Collections.Generic
                            @using System.Threading.Tasks
                            @using Microsoft.AspNetCore.Mvc
                            ");
                            builder.ConfigureClass(
                                (document, @class) =>
                                {
                                    @class.BaseType = BaseType;
                                    var relativePath = document.Source.RelativePath;
                                    @class.ClassName = (string)sanitizeIdentifier.Invoke(null, new[] { relativePath.Substring(0, relativePath.Length - ".cshtml".Length) });
                                });
                        });

                var generateFiles = new List<ITaskItem>();
                foreach (var projectItem in engine.FileSystem.EnumerateItems("/"))
                {
                    var relativePath = projectItem.RelativePhysicalPath;
                    var className = (string)sanitizeIdentifier.Invoke(null, new object[] { relativePath.Substring(0, relativePath.Length - ".cshtml".Length) });
                    var generateFile = Path.Combine(GeneratePath, $"{className}.g.cs");
                    var code = engine.Process(projectItem).GetCSharpDocument().GeneratedCode;
                    File.WriteAllText(generateFile, code, Encoding.UTF8);
                    generateFiles.Add(new TaskItem(generateFile));
                }

                GenerateFiles = generateFiles.ToArray();
                return true;
            }
        }
    }
}
