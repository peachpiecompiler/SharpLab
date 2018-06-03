using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using MirrorSharp.Advanced;

namespace SharpLab.Server.Compilation {
    public interface ICompiler {
        [NotNull]
        Task<bool> TryCompileToStreamAsync([NotNull] MemoryStream assemblyStream, [CanBeNull] MemoryStream symbolStream, [CanBeNull] MemoryStream xmlDocStream, [NotNull] IWorkSession session, [NotNull, ItemNotNull] IList<Diagnostic> diagnostics, CancellationToken cancellationToken);
    }
}