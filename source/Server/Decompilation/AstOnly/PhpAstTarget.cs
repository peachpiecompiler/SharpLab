using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devsense.PHP.Syntax;
using Devsense.PHP.Syntax.Ast;
using MirrorSharp.Advanced;
using MirrorSharp.Php.Advanced;
using SharpLab.Server.Decompilation.Internal;

namespace SharpLab.Server.Decompilation.AstOnly
{
    public class PhpAstTarget : IAstTarget {
        private class SerializerVisitor : TreeVisitor {
            private readonly IFastJsonWriter _writer;

            /// <summary>Helper field to enable lazy adding of the 'children' property.</summary>
            private bool _firstChild = false;

            public SerializerVisitor(IFastJsonWriter writer) {
                _writer = writer;
            }

            public override void VisitElement(LangElement element) {
                if (element != null) {
                    if (_firstChild) {
                        // If this element is the first child of another one, start writing its 'children' property
                        _writer.WritePropertyStartArray("children");
                    }

                    _writer.WriteStartObject();

                    _writer.WriteProperty("type", "node");
                    _writer.WriteProperty("kind", element.GetType().Name);

                    _writer.WritePropertyName("range");
                    _writer.WriteValueFromParts(element.Span.Start, '-', element.Span.End);

                    // Visit children
                    _firstChild = true;
                    element.VisitMe(this);
                    if (!_firstChild) {
                        // If there was a child of this element written, enclose the 'children' property array
                        _writer.WriteEndArray();
                    }

                    // State that the first child of the current parrent has already been visited
                    // (it also signals that the current parrent contains any children at all)
                    _firstChild = false;

                    _writer.WriteEndObject();
                }
            }
        }

        public Task<object> GetAstAsync(IWorkSession session, CancellationToken cancellationToken) {
            var syntaxRoot = session.Php().Compilation.SyntaxTrees.Single().Root;
            return Task.FromResult<object>(syntaxRoot);
        }

        public void SerializeAst(object ast, IFastJsonWriter writer, IWorkSession session) {
            writer.WriteStartArray();
            var visitor = new SerializerVisitor(writer);
            visitor.VisitElement((LangElement)ast);
            writer.WriteEndArray();
        }

        public IReadOnlyCollection<string> SupportedLanguageNames { get; } = new[] { "PHP" };
    }
}
