using System.Collections.Concurrent;
using System.IO;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using SharpLab.Server.Common;

namespace SharpLab.Server.Decompilation {
    public class CSharpDecompiler : IDecompiler {
        private static readonly ConcurrentDictionary<string, AssemblyDefinition> AssemblyCache = new ConcurrentDictionary<string, AssemblyDefinition>();
        private static readonly CSharpFormattingOptions FormattingOptions = FormattingOptionsFactory.CreateKRStyle();
        private static readonly DecompilerSettings DecompilerSettings = new DecompilerSettings {
            //CanInlineVariables = false,
            //OperatorOverloading = false,
            AnonymousMethods = false,
            AnonymousTypes = false,
            YieldReturn = false,
            AsyncAwait = false,
            AutomaticProperties = false,
            ExpressionTrees = false,
            //ArrayInitializers = false,
            ObjectOrCollectionInitializers = false,
            //LiftedOperators = false,
            UsingStatement = false
        };

        public void Decompile(Stream assemblyStream, TextWriter codeWriter) {
            // ReSharper disable once AgentHeisenbug.CallToNonThreadSafeStaticMethodInThreadSafeType
            var module = ModuleDefinition.ReadModule(assemblyStream, new ReaderParameters {
                AssemblyResolver = PreCachedAssemblyResolver.Instance
            });

            var decompiler = new ICSharpCode.Decompiler.CSharp.CSharpDecompiler(module, DecompilerSettings);
            var syntaxTree = decompiler.Decompile();

            new CSharpOutputVisitor(codeWriter, FormattingOptions).VisitSyntaxTree(syntaxTree);
        }

        public string LanguageName => TargetNames.CSharp;
    }
}