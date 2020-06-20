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

                    // Start visiting children
                    _firstChild = true;
                    if (element is Expression expr) {
                        // The expr.Operation value will be the first child
                        _writer.WritePropertyStartArray("children");
                        _firstChild = false;

                        SerializeToken(nameof(expr.Operation), expr.Operation.ToString(), null);
                    }

                    // Element specific children
                    element.VisitMe(this);

                    // End visiting children
                    if (!_firstChild) {
                        // If there was a child of this element written, enclose the 'children' property array
                        _writer.WriteEndArray();
                    }

                    // State that the first child of the current parent has already been visited
                    // (it also signals that the current parrent contains any children at all)
                    _firstChild = false;

                    _writer.WriteEndObject();
                }
            }

            public override void VisitNamespaceDecl(NamespaceDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.QualifiedName), x.QualifiedName.ToString(), x.QualifiedName.Span);
                SerializeToken(nameof(x.IsAnonymous), x.IsAnonymous.ToString(), null);

                base.VisitNamespaceDecl(x);
            }

            public override void VisitGlobalConstantDecl(GlobalConstantDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.Name.Span);
                SerializeToken(nameof(x.IsConditional), x.IsConditional.ToString(), null);

                base.VisitGlobalConstantDecl(x);
            }

            public override void VisitUseStatement(UseStatement x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Kind), x.Kind.ToString(), null);
                x.Uses.Foreach(SerializeUse);

                base.VisitUseStatement(x);
            }

            private void SerializeUse(UseBase use) {
                _writer.WriteStartObject();
                _writer.WriteProperty("type", "node");
                _writer.WriteProperty("kind", use.GetType().Name);
                SerializeSpanProperty(use.Span);

                _writer.WritePropertyStartArray("children");
                if (use is SimpleUse simpleUse) {
                    SerializeToken(nameof(simpleUse.HasSeparateAlias), simpleUse.HasSeparateAlias.ToString(), null);
                    SerializeToken(nameof(simpleUse.QualifiedName), simpleUse.QualifiedName.ToString(), simpleUse.NameSpan);
                    SerializeToken(nameof(simpleUse.Alias), simpleUse.Alias.Name.ToString(), simpleUse.AliasSpan);
                }
                else {
                    var groupUse = (GroupUse)use;
                    groupUse.Uses.Foreach(SerializeUse);
                }
                _writer.WriteEndArray();

                _writer.WriteEndObject();
            }

            public override void VisitTypeDecl(TypeDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.Name.Span);
                SerializeToken(nameof(x.BaseClass), x.BaseClass?.ClassName.ToString(), x.BaseClass?.Span);
                SerializeToken(nameof(x.MemberAttributes), x.MemberAttributes.ToString(), x.HeadingSpan);
                SerializeTokenList(nameof(x.ImplementsList), x.ImplementsList, impl => impl.ClassName.ToString(), impl => impl.Span);
                SerializeToken(nameof(x.IsConditional), x.IsConditional.ToString(), null);

                base.VisitTypeDecl(x);
            }

            public override void VisitMethodDecl(MethodDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.Name.Span);

                base.VisitMethodDecl(x);
            }

            public override void VisitFieldDeclList(FieldDeclList x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Modifiers), x.Modifiers.ToString(), x.Span);

                base.VisitFieldDeclList(x);
            }

            public override void VisitFieldDecl(FieldDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.NameSpan);
                SerializeToken(nameof(x.HasInitVal), x.HasInitVal.ToString(), null);

                base.VisitFieldDecl(x);
            }

            public override void VisitConstantDecl(ConstantDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.Name.Span);

                base.VisitConstantDecl(x);
            }

            public override void VisitFunctionDecl(FunctionDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.Name.Span);

                base.VisitFunctionDecl(x);
            }

            public override void VisitTraitAdaptationPrecedence(TraitsUse.TraitAdaptationPrecedence x) {
                VisitSpecificElementProlog();

                if (x.TraitMemberName.Item2.HasValue) {
                    SerializeToken(nameof(x.TraitMemberName), x.TraitMemberName.Item2.ToString(), x.TraitMemberName.Item2.Span);
                }

                SerializeTokenList(nameof(x.IgnoredTypes), x.IgnoredTypes, type => type.QualifiedName?.ToString(), type => type.Span);

                base.VisitTraitAdaptationPrecedence(x);
            }

            public override void VisitTraitAdaptationAlias(TraitsUse.TraitAdaptationAlias x) {
                VisitSpecificElementProlog();

                if (x.TraitMemberName.Item2.HasValue) {
                    SerializeToken(nameof(x.TraitMemberName), x.TraitMemberName.Item2.ToString(), x.TraitMemberName.Item2.Span);
                }
                if (x.NewModifier != null) {
                    SerializeToken(nameof(x.NewModifier), x.NewModifier.ToString(), null);
                }
                SerializeToken(nameof(x.NewName), x.NewName.ToString(), x.NewName.Span);

                base.VisitTraitAdaptationAlias(x);
            }

            public override void VisitGotoStmt(GotoStmt x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.LabelName), x.LabelName.ToString(), x.LabelName.Span);

                base.VisitGotoStmt(x);
            }

            public override void VisitDirectVarUse(DirectVarUse x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.VarName), x.VarName.ToString(), x.Span);

                base.VisitDirectVarUse(x);
            }

            public override void VisitGlobalConstUse(GlobalConstUse x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.Span);

                base.VisitGlobalConstUse(x);
            }

            public override void VisitClassConstUse(ClassConstUse x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), x.NamePosition);

                base.VisitClassConstUse(x);
            }

            public override void VisitPseudoClassConstUse(PseudoClassConstUse x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Type), x.Type.ToString(), x.NamePosition);

                base.VisitPseudoClassConstUse(x);
            }

            public override void VisitPseudoConstUse(PseudoConstUse x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Type), x.Type.ToString(), x.Span);

                base.VisitPseudoConstUse(x);
            }

            public override void VisitIncludingEx(IncludingEx x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.InclusionType), x.InclusionType.ToString(), null);
                SerializeToken(nameof(x.IsConditional), x.IsConditional.ToString(), null);

                base.VisitIncludingEx(x);
            }

            public override void VisitItemUse(ItemUse x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.IsBraces), x.IsBraces.ToString(), null);
                SerializeToken(nameof(x.IsFunctionArrayDereferencing), x.IsFunctionArrayDereferencing.ToString(), null);

                base.VisitItemUse(x);
            }

            public override void VisitDirectFcnCall(DirectFcnCall x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.FullName), x.FullName.Name.ToString(), x.NameSpan);

                base.VisitDirectFcnCall(x);
            }

            public override void VisitDirectStMtdCall(DirectStMtdCall x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.MethodName), x.MethodName.ToString(), x.NameSpan);

                base.VisitDirectStMtdCall(x);
            }

            public override void VisitArrayEx(ArrayEx x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.IsShortSyntax), x.IsShortSyntax.ToString(), null);

                foreach (var item in x.Items) {
                    if (item != null) {
                        string kind;
                        LangElement valChild;
                        if (item is ValueItem valItem) {
                            kind = nameof(ValueItem);
                            valChild = valItem.ValueExpr;
                        } else {
                            kind = nameof(RefItem);
                            valChild = ((RefItem)item).RefToGet;
                        }

                        _writer.WriteStartObject();
                        _writer.WriteProperty("type", "node");
                        _writer.WriteProperty("kind", kind);

                        Span span;
                        if (item.Index != null) {
                            int spanStart = item.Index.Span.Start;
                            span = new Span(spanStart, valChild.Span.End - spanStart);
                        } else {
                            span = valChild.Span;
                        }
                        SerializeSpanProperty(span);

                        _writer.WritePropertyStartArray("children");
                        SerializeToken(nameof(item.IsByRef), item.IsByRef.ToString(), null);
                        VisitElement(item.Index);
                        VisitElement(valChild);
                        _writer.WriteEndArray();

                        _writer.WriteEndObject();
                    }
                }
            }

            public override void VisitIncDecEx(IncDecEx x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Inc), x.Inc.ToString(), null);
                SerializeToken(nameof(x.Post), x.Post.ToString(), null);

                base.VisitIncDecEx(x);
            }

            public override void VisitLambdaFunctionExpr(LambdaFunctionExpr x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Modifiers), x.Modifiers.ToString(), x.HeadingSpan);

                base.VisitLambdaFunctionExpr(x);
            }

            public override void VisitLongIntLiteral(LongIntLiteral x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Value), x.Value.ToString(), x.Span);
            }

            public override void VisitDoubleLiteral(DoubleLiteral x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Value), x.Value.ToString(), x.Span);
            }

            public override void VisitStringLiteral(StringLiteral x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Value), x.Value.ToString(), x.Span);
            }

            public override void VisitBinaryStringLiteral(BinaryStringLiteral x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Value), x.Value.ToString(), x.Span);
            }

            public override void VisitBoolLiteral(BoolLiteral x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Value), x.Value.ToString(), x.Span);
            }

            public override void VisitStaticVarDecl(StaticVarDecl x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Variable), x.Variable.ToString(), x.NameSpan);

                base.VisitStaticVarDecl(x);
            }

            public override void VisitFormalParam(FormalParam x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Name), x.Name.ToString(), null);
                SerializeToken(nameof(x.PassedByRef), x.PassedByRef.ToString(), null);
                SerializeToken(nameof(x.IsOut), x.IsOut.ToString(), null);
                SerializeToken(nameof(x.IsVariadic), x.IsVariadic.ToString(), null);

                base.VisitFormalParam(x);
            }

            public override void VisitActualParam(ActualParam x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Ampersand), x.Ampersand.ToString(), null);
                SerializeToken(nameof(x.IsUnpack), x.IsUnpack.ToString(), null);

                base.VisitActualParam(x);
            }

            //public override void VisitNamedActualParam(NamedActualParam x) {
            //    VisitSpecificElementProlog();

            //    SerializeToken(nameof(x.Name), x.Name.ToString(), null);

            //    base.VisitNamedActualParam(x);
            //}

            public override void VisitPrimitiveTypeRef(PrimitiveTypeRef x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.PrimitiveTypeName), x.PrimitiveTypeName.ToString(), x.Span);

                base.VisitPrimitiveTypeRef(x);
            }

            public override void VisitClassTypeRef(ClassTypeRef x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.ClassName), x.ClassName.ToString(), x.Span);

                base.VisitClassTypeRef(x);
            }

            public override void VisitTranslatedTypeRef(TranslatedTypeRef x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.ClassName), x.ClassName.ToString(), null);

                base.VisitTranslatedTypeRef(x);
            }

            public override void VisitReservedTypeRef(ReservedTypeRef x) {
                VisitSpecificElementProlog();

                SerializeToken(nameof(x.Type), x.Type.ToString(), x.Span);

                base.VisitReservedTypeRef(x);
            }

            private void VisitSpecificElementProlog() {
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
