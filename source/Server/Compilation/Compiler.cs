using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Compiler;
using Microsoft.FSharp.Compiler.SourceCodeServices;
using Microsoft.FSharp.Control;
using MirrorSharp.Advanced;
using MirrorSharp.FSharp.Advanced;
using MirrorSharp.Php.Advanced;

namespace SharpLab.Server.Compilation {
    public class Compiler : ICompiler {
        public async Task<bool> TryCompileToStreamAsync(MemoryStream assemblyStream, MemoryStream symbolStream, MemoryStream xmlDocStream, IWorkSession session, IList<Diagnostic> diagnostics, CancellationToken cancellationToken) {
            if (session.IsFSharp())
                return await TryCompileFSharpToStreamAsync(assemblyStream, session, diagnostics, cancellationToken);

            if (session.IsPhp())
                return TryCompilePhpToStreamAsync(assemblyStream, symbolStream, xmlDocStream, session, diagnostics);

            var compilation = await session.Roslyn.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var emitResult = compilation.Emit(assemblyStream, pdbStream: symbolStream, xmlDocumentationStream: xmlDocStream);
            if (!emitResult.Success) {
                foreach (var diagnostic in emitResult.Diagnostics) {
                    diagnostics.Add(diagnostic);
                }
                return false;
            }
            return true;
        }

        private bool TryCompilePhpToStreamAsync(MemoryStream assemblyStream, MemoryStream symbolStream, MemoryStream xmlDocStream, IWorkSession session, IList<Diagnostic> diagnostics) {
            var compilation = session.Php().Compilation;

            // TODO: Remove this workaround when it is fixed in Peachpie
            // We need to store the XML documentation to a separate stream because PhpCompilation.Emit() closes the doc stream
            using (var dummyDocStream = new MemoryStream()) {
                var emitResult = compilation.Emit(assemblyStream, symbolStream, dummyDocStream);
                if (!emitResult.Success) {
                    foreach (var diagnostic in emitResult.Diagnostics) {
                        diagnostics.Add(diagnostic.ToStandardRoslyn());
                    }
                    return false;
                }

                // ToArray() is possible even on a closed stream
                var docBytes = dummyDocStream.ToArray();
                xmlDocStream.Write(docBytes, 0, docBytes.Length);
            }
            return true;
        }

        private async Task<bool> TryCompileFSharpToStreamAsync(MemoryStream assemblyStream, IWorkSession session, IList<Diagnostic> diagnostics, CancellationToken cancellationToken) {
            var fsharp = session.FSharp();

            // GetLastParseResults are guaranteed to be available here as MirrorSharp's SlowUpdate does the parse
            var parsed = fsharp.GetLastParseResults();
            using (var virtualAssemblyFile = FSharpFileSystem.RegisterVirtualFile(assemblyStream)) {
                var compiled = await FSharpAsync.StartAsTask(fsharp.Checker.Compile(
                    // ReSharper disable once PossibleNullReferenceException
                    FSharpList<Ast.ParsedInput>.Cons(parsed.ParseTree.Value, FSharpList<Ast.ParsedInput>.Empty),
                    "_", virtualAssemblyFile.Name,
                    fsharp.AssemblyReferencePathsAsFSharpList,
                    pdbFile: null,
                    executable: false,//fsharp.ProjectOptions.OtherOptions.Contains("--target:exe"),
                    noframework: true,
                    userOpName: null
                ), null, cancellationToken).ConfigureAwait(false);
                foreach (var error in compiled.Item1) {
                    // no reason to add warnings as check would have added them anyways
                    if (error.Severity.Tag == FSharpErrorSeverity.Tags.Error)
                        diagnostics.Add(fsharp.ConvertToDiagnostic(error));
                }
                return virtualAssemblyFile.Stream.Length > 0;
            }
        }
    }
}
