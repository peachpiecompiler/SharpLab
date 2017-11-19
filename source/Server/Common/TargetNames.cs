using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace SharpLab.Server.Common {
    public static class TargetNames {
        public const string CSharp = LanguageNames.CSharp;
        public const string Ast = "AST";
        public const string JitAsm = "JIT ASM";
        public const string Run = "Run";
    }
}
