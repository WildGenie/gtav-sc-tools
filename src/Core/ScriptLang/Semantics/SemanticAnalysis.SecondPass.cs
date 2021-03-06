﻿#nullable enable
namespace ScTools.ScriptLang.Semantics
{
    using System;
    using System.Diagnostics;
    using System.Linq;

    using ScTools.ScriptLang.Ast;
    using ScTools.ScriptLang.Semantics.Symbols;

    public static partial class SemanticAnalysis
    {
        /// <summary>
        /// Register local symbols inside procedures/functions and check that expressions types are correct.
        /// </summary>
        private sealed class SecondPass : Pass
        {
            private DefinedFunctionSymbol? func = null;

            public SecondPass(DiagnosticsReport diagnostics, string filePath, SymbolTable symbols)
                : base(diagnostics, filePath, symbols)
            { }

            protected override void OnEnd()
            {
                Debug.Assert(Symbols.Parent == null);
                Debug.Assert(Symbols.Symbols.Where(sym => sym is VariableSymbol)
                                            .Cast<VariableSymbol>()
                                            .All(s => s.Kind is VariableKind.Static or VariableKind.Constant),
                             "All variables in global scope must be static or constant");
            }

            public override void VisitFunctionStatement(FunctionStatement node)
            {
                var funcSymbol = Symbols.Lookup(node.Name) as DefinedFunctionSymbol;
                Debug.Assert(funcSymbol != null);

                VisitFunc(funcSymbol, node.ParameterList, node.Block);
            }

            public override void VisitProcedureStatement(ProcedureStatement node)
            {
                var funcSymbol = Symbols.Lookup(node.Name) as DefinedFunctionSymbol;
                Debug.Assert(funcSymbol != null);

                VisitFunc(funcSymbol, node.ParameterList, node.Block);
            }

            private void VisitFunc(DefinedFunctionSymbol func, ParameterList parameters, StatementBlock block)
            {
                this.func = func;

                Symbols = Symbols.EnterScope(block);
                parameters.Accept(this);
                block.Accept(this);
                Symbols = Symbols.ExitScope();
            }

            public override void VisitProcedurePrototypeStatement(ProcedurePrototypeStatement node)
            {
                // empty to avoid visiting its ParameterList
            }

            public override void VisitFunctionPrototypeStatement(FunctionPrototypeStatement node)
            {
                // empty to avoid visiting its ParameterList
            }

            public override void VisitProcedureNativeStatement(ProcedureNativeStatement node)
            {
                // empty to avoid visiting its ParameterList
            }

            public override void VisitFunctionNativeStatement(FunctionNativeStatement node)
            {
                // empty to avoid visiting its ParameterList
            }

            public override void VisitStaticVariableStatement(StaticVariableStatement node)
            {
                foreach (var decl in node.Declaration.Declarators)
                {
                    var v = Symbols.Lookup(decl.Declarator.Identifier) as VariableSymbol;
                    Debug.Assert(v != null);
                    Debug.Assert(v.IsStatic);

                    if (v.Type is RefType)
                    {
                        Diagnostics.AddError(FilePath, $"Static variables cannot be reference types", decl.Declarator.Source);
                    }

                    if (decl.Initializer != null)
                    {
                        if (v.Type is BasicType { TypeCode: BasicTypeCode.String })
                        {
                            Diagnostics.AddError(FilePath, $"Static variables of type STRING cannot have an initializer", decl.Initializer.Source);
                        }

                        var initializerType = TypeOf(decl.Initializer);
                        if (initializerType == null || !v.Type.IsAssignableFrom(initializerType, considerReferences: false))
                        {
                            Diagnostics.AddError(FilePath, $"Mismatched initializer type and type of static variable '{v.Name}'", decl.Initializer.Source);
                        }
                    }
                }
            }

            public override void VisitConstantVariableStatement(ConstantVariableStatement node)
            {
                //foreach (var decl in node.Declaration.Declarators)
                //{
                //    var v = Symbols.Lookup(decl.Declarator.Identifier) as VariableSymbol;
                //    Debug.Assert(v != null);
                //    Debug.Assert(v.IsConstant);
                //}
            }

            public override void VisitParameterList(ParameterList node)
            {
                foreach (var p in node.Parameters)
                {
                    var v = new VariableSymbol(p.Declarator.Declarator.Identifier,
                                               p.Source,
                                               TryResolveVarDecl(p.Type, p.Declarator.Declarator),
                                               VariableKind.LocalArgument);
                    Symbols.Add(v);
                    func?.LocalArgs.Add(v);
                }
            }

            public override void VisitVariableDeclarationStatement(VariableDeclarationStatement node)
            {
                foreach (var decl in node.Declaration.Declarators)
                {
                    var v = new VariableSymbol(decl.Declarator.Identifier,
                                               node.Source,
                                               TryResolveVarDecl(node.Declaration.Type, decl.Declarator),
                                               VariableKind.Local);

                    if (decl.Initializer != null)
                    {
                        var initializerType = TypeOf(decl.Initializer);
                        if (initializerType == null || !v.Type.IsAssignableFrom(initializerType, considerReferences: true))
                        {
                            Diagnostics.AddError(FilePath, $"Mismatched initializer type and type of variable '{v.Name}'", decl.Initializer.Source);
                        }
                    }

                    Symbols.Add(v);
                    func?.Locals.Add(v);
                }
            }

            public override void VisitAssignmentStatement(AssignmentStatement node)
            {
                var destType = TypeOf(node.Left);
                var srcType = TypeOf(node.Right);
            
                if (destType == null || srcType == null)
                {
                    return;
                }

                if (!destType.IsAssignableFrom(srcType, considerReferences: true))
                {
                    Diagnostics.AddError(FilePath, "Mismatched types in assigment", node.Source);
                }

                if (destType is RefType refTy && refTy.ElementType is AnyType)
                {
                    Diagnostics.AddError(FilePath, "Cannot modify references of type ANY", node.Source);
                }
            }

            public override void VisitIfStatement(IfStatement node)
            {
                var conditionType = TypeOf(node.Condition);
                if (conditionType?.UnderlyingType is not BasicType { TypeCode: BasicTypeCode.Bool })
                {
                    Diagnostics.AddError(FilePath, $"IF statement condition requires BOOL type", node.Condition.Source);
                }

                Symbols = Symbols.EnterScope(node.ThenBlock);
                node.ThenBlock.Accept(this);
                Symbols = Symbols.ExitScope();

                if (node.ElseBlock != null)
                {
                    Symbols = Symbols.EnterScope(node.ElseBlock);
                    node.ElseBlock.Accept(this);
                    Symbols = Symbols.ExitScope();
                }
            }

            public override void VisitWhileStatement(WhileStatement node)
            {
                var conditionType = TypeOf(node.Condition);
                if (conditionType?.UnderlyingType is not BasicType { TypeCode: BasicTypeCode.Bool })
                {
                    Diagnostics.AddError(FilePath, $"WHILE statement condition requires BOOL type", node.Condition.Source);
                }

                Symbols = Symbols.EnterScope(node.Block);
                node.Block.Accept(this);
                Symbols = Symbols.ExitScope();
            }

            public override void VisitRepeatStatement(RepeatStatement node)
            {
                var limitType = TypeOf(node.Limit);
                if (limitType?.UnderlyingType is not BasicType { TypeCode: BasicTypeCode.Int })
                {
                    Diagnostics.AddError(FilePath, $"REPEAT statement limit requires INT type", node.Limit.Source);
                }

                var counterType = TypeOf(node.Counter);
                if (counterType?.UnderlyingType is not BasicType { TypeCode: BasicTypeCode.Int })
                {
                    Diagnostics.AddError(FilePath, $"REPEAT statement counter requires INT type", node.Counter.Source);
                }

                Symbols = Symbols.EnterScope(node.Block);
                node.Block.Accept(this);
                Symbols = Symbols.ExitScope();
            }

            public override void VisitSwitchStatement(SwitchStatement node)
            {
                var exprType = TypeOf(node.Expression);
                if (exprType?.UnderlyingType is not BasicType { TypeCode: BasicTypeCode.Int })
                {
                    Diagnostics.AddError(FilePath, $"SWITCH statement value requires INT type", node.Expression.Source);
                }

                DefaultVisit(node);
            }

            public override void VisitValueSwitchCase(ValueSwitchCase node)
            {
                var valueType = TypeOf(node.Value);
                if (valueType is not BasicType { TypeCode: BasicTypeCode.Int })
                {
                    Diagnostics.AddError(FilePath, $"SWITCH case value requires INT type", node.Value.Source);
                }

                Symbols = Symbols.EnterScope(node.Block);
                node.Block.Accept(this);
                Symbols = Symbols.ExitScope();
            }

            public override void VisitDefaultSwitchCase(DefaultSwitchCase node)
            {
                Symbols = Symbols.EnterScope(node.Block);
                node.Block.Accept(this);
                Symbols = Symbols.ExitScope();
            }

            public override void VisitReturnStatement(ReturnStatement node)
            {
                Debug.Assert(func != null);

                if (node.Expression == null)
                {
                    // if this is a function, the missing expression should have been reported by SyntaxChecker
                    return;
                }

                var returnType = TypeOf(node.Expression);
                if (returnType == null || !func.Type.ReturnType!.IsAssignableFrom(returnType, considerReferences: true))
                {
                    Diagnostics.AddError(FilePath, $"Returned type does not match the specified function return type", node.Expression.Source);
                }
            }

            public override void VisitInvocationStatement(InvocationStatement node)
            {
                // TODO: very similar to TypeOf.VisitInvocationExpression, refactor
                var callableType = TypeOf(node.Expression);
                if (callableType is not FunctionType f)
                {
                    if (callableType != null)
                    {
                        Diagnostics.AddError(FilePath, $"Cannot call '{node.Expression}', it is not a procedure or a function", node.Expression.Source);
                    }
                    return;
                }

                int expected = f.ParameterCount;
                int found = node.ArgumentList.Arguments.Length;
                if (found != expected)
                {
                    Diagnostics.AddError(FilePath, $"Mismatched number of arguments. Expected {expected}, found {found}", node.ArgumentList.Source);
                }

                int argCount = Math.Min(expected, found);
                for (int i = 0; i < argCount; i++)
                {
                    var foundType = TypeOf(node.ArgumentList.Arguments[i]);
                    if (!f.DoesParameterTypeMatch(i, foundType))
                    {
                        Diagnostics.AddError(FilePath, $"Mismatched type of argument #{i}", node.ArgumentList.Arguments[i].Source);
                    }
                }
            }

            public override void DefaultVisit(Node node)
            {
                foreach (var n in node.Children)
                {
                    n.Accept(this);
                }
            }
        }
    }
}
