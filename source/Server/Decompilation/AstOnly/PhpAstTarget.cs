using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devsense.PHP.Syntax;
using Devsense.PHP.Syntax.Ast;
using Devsense.PHP.Text;
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
                    SerializeSpanProperty(element.Span);

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

            public override void VisitTypeDecl(TypeDecl x) {
                VisitNodeProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.Name.Span);
                SerializeToken(nameof(x.BaseClass), x.BaseClass?.ClassName.ToString(), x.BaseClass?.Span);
                SerializeToken(nameof(x.MemberAttributes), x.MemberAttributes.ToString(), x.HeadingSpan);
                SerializeTokenList(nameof(x.ImplementsList), x.ImplementsList, impl => impl.ClassName.ToString(), impl => impl.Span);
                SerializeToken(nameof(x.IsConditional), x.IsConditional.ToString(), null);

                base.VisitTypeDecl(x);
            }

            private void VisitNodeProlog() {
                if (_firstChild) {
                    // If this element is the first child of another one, start writing its 'children' property
                    _writer.WritePropertyStartArray("children");

                    // Do not repeat the 'children' property header in the children nodes
                    _firstChild = false;
                }
            }

            private void SerializeToken(string name, string value, Span? span) {
                if (value != null) {
                    _writer.WriteStartObject();
                    _writer.WriteProperty("type", span.HasValue ? "token" : "value");
                    if (name != null) {
                        _writer.WriteProperty("property", name);
                    }
                    _writer.WriteProperty("value", value);
                    if (span != null) {
                        SerializeSpanProperty(span.Value);
                    }
                    _writer.WriteEndObject();
                }
            }

            private void SerializeTokenList<T>(string name, IEnumerable<T> items, Func<T, string> valueSelector, Func<T, Span> spanSelector = null) {
                if (items.Any()) {
                    _writer.WriteStartObject();
                    _writer.WriteProperty("type", (spanSelector != null) ? "token" : "value");
                    _writer.WriteProperty("property", name);

                    if (spanSelector != null) {
                        int minStart = items.Min(item => spanSelector(item).Start);
                        int maxEnd = items.Max(item => spanSelector(item).End);
                        var span = new Span(minStart, maxEnd - minStart);
                        SerializeSpanProperty(span);
                    }

                    _writer.WritePropertyStartArray("children");
                    foreach (var item in items) {
                        SerializeToken(null, valueSelector(item), spanSelector?.Invoke(item));
                    }
                    _writer.WriteEndArray();
                    _writer.WriteEndObject();
                }
            }

            private void SerializeSpanProperty(Span span) {
                _writer.WritePropertyName("range");
                _writer.WriteValueFromParts(span.Start, '-', span.End);
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
