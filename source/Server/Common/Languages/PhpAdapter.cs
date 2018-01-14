extern alias peachpie;
using PeachpieRoslyn = peachpie::Microsoft.CodeAnalysis;

using System.Collections.Immutable;
using JetBrains.Annotations;
using MirrorSharp;
using MirrorSharp.Advanced;
using MirrorSharp.Php.Advanced;
using Pchp.CodeAnalysis;

namespace SharpLab.Server.Common.Languages
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
    public class PhpAdapter : ILanguageAdapter {
        public string LanguageName => "PHP";

        public void SlowSetup([NotNull] MirrorSharpOptions options) {
            options.EnablePhp();
        }

        public void SetOptimize([NotNull] IWorkSession session, [NotNull] string optimize) {
            var optimizationLevel = (optimize == Optimize.Debug) ? PeachpieRoslyn.OptimizationLevel.Debug : PeachpieRoslyn.OptimizationLevel.Release;

            IPhpSession php = session.Php();
            var compilation = php.Compilation;
            php.Compilation = (PhpCompilation)compilation.WithOptions(compilation.Options.WithOptimizationLevel(optimizationLevel));
        }

        public void SetOptionsForTarget([NotNull] IWorkSession session, [NotNull] string target) {
            var outputKind = target != TargetNames.Run ? PeachpieRoslyn.OutputKind.DynamicallyLinkedLibrary : PeachpieRoslyn.OutputKind.ConsoleApplication;

            IPhpSession php = session.Php();
            var compilation = php.Compilation;
            php.Compilation = (PhpCompilation)compilation.WithOptions(compilation.Options.WithOutputKind(outputKind));
        }

        public ImmutableArray<int> GetMethodParameterLines([NotNull] IWorkSession session, int lineInMethod, int columnInMethod) {
            return ImmutableArray<int>.Empty; // not supported yet
        }
    }
}
