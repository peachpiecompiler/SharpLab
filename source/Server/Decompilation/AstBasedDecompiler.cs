using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using AshMind.Extensions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Ast;
using ICSharpCode.NRefactory.CSharp;
using Mono.Cecil;
using SharpLab.Server.Common;

namespace SharpLab.Server.Decompilation {
    public abstract class AstBasedDecompiler : IDecompiler {
        private static readonly ConcurrentDictionary<string, AssemblyDefinition> AssemblyCache = new ConcurrentDictionary<string, AssemblyDefinition>();

        public void Decompile(Stream assemblyStream, TextWriter codeWriter) {
            // ReSharper disable once AgentHeisenbug.CallToNonThreadSafeStaticMethodInThreadSafeType
            var module = ModuleDefinition.ReadModule(assemblyStream, new ReaderParameters {
                AssemblyResolver = PreCachedAssemblyResolver.Instance
            });

            var context = new DecompilerContext(module) {
                Settings = {
                    CanInlineVariables = false,
                    OperatorOverloading = false,
                    AnonymousMethods = false,
                    AnonymousTypes = false,
                    YieldReturn = false,
                    AsyncAwait = false,
                    AutomaticProperties = false,
                    ExpressionTrees = false,
                    ArrayInitializers = false,
                    ObjectOrCollectionInitializers = false,
                    LiftedOperators = false,
                    UsingStatement = false
                }
            };

            var ast = new AstBuilder(context);
            ast.AddAssembly(module.Assembly);

            // Remove a large helper class generated by Peachpie
            ast.SyntaxTree.Children
                .OfType<TypeDeclaration>()
                .Where(decl => decl.Name == Pchp.Core.Context.ScriptInfo.ScriptTypeName)
                .FirstOrDefault()
                ?.Remove();

            WriteResult(codeWriter, ast);
        }

        protected abstract void WriteResult(TextWriter writer, AstBuilder ast);

        public abstract string LanguageName { get; }
    }
}