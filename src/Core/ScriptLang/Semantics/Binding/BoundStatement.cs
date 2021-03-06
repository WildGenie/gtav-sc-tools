﻿#nullable enable
namespace ScTools.ScriptLang.Semantics.Binding
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;

    using ScTools.ScriptLang.CodeGen;
    using ScTools.ScriptLang.Semantics.Symbols;

    public abstract class BoundStatement : BoundNode
    {
        public abstract void Emit(ByteCodeBuilder code, BoundFunction parent);
    }

    public sealed class BoundInvalidStatement : BoundStatement
    {
        public override void Emit(ByteCodeBuilder code, BoundFunction parent) => throw new NotSupportedException();
    }

    public sealed class BoundVariableDeclarationStatement : BoundStatement
    {
        public VariableSymbol Var { get; }

        public BoundVariableDeclarationStatement(VariableSymbol var)
        {
            if (var.Kind != VariableKind.Local)
            {
                throw new ArgumentException("Only local variables can have variable declaration statements", nameof(var));
            }

            if (var.Type is RefType)
            {
                var initializer = var.Initializer;
                if (initializer == null)
                {
                    throw new ArgumentException("Initializer is missing for reference type", nameof(var));
                }

                if (!initializer.IsAddressable)
                {
                    throw new ArgumentException("Initializer is not addressable and var is a reference", nameof(var));
                }
            }

            Var = var;
        }

        public override void Emit(ByteCodeBuilder code, BoundFunction parent)
        {
            EmitArrayLengthInitializers(Var.Type, parent.GetLocalLocation(Var)!.Value, code);
            var initializer = Var.Initializer;
            if (initializer != null)
            {
                if (Var.Type is RefType ||
                    (Var.Type is BasicType { TypeCode: BasicTypeCode.String } && initializer.Type?.UnderlyingType is TextLabelType))
                {
                    initializer.EmitAddr(code);
                }
                else
                {
                    initializer.EmitLoad(code);
                }
                code.EmitLocalStore(Var);
            }
        }

        private static void EmitArrayLengthInitializers(Semantics.Type ty, int offset, ByteCodeBuilder code)
        {
            switch (ty)
            {
                case ArrayType arrTy:
                    // this is how R*'s compiler initializes array, though it could be simplified (PUSH->LOCAL->STORE)
                    code.EmitLocalAddr(offset);
                    code.EmitPushInt(arrTy.Length);
                    code.EmitAddrStoreRev();
                    code.EmitDrop();

                    if (arrTy.ItemType is ArrayType or StructType)
                    {
                        var itemSize = arrTy.ItemType.SizeOf;
                        offset += 1;
                        for (int i = 0; i < arrTy.Length; i++)
                        {
                            EmitArrayLengthInitializers(arrTy.ItemType, offset, code);
                            offset += itemSize;
                        }
                    }
                    break;
                case StructType strucTy:
                    for (int i = 0; i < strucTy.Fields.Count; i++)
                    {
                        var f = strucTy.Fields[i];
                        EmitArrayLengthInitializers(f.Type, offset, code);
                        offset += f.Type.SizeOf;
                    }
                    break;
                default: break;
            }
        }
    }

    public sealed class BoundAssignmentStatement : BoundStatement
    {
        public BoundExpression Left { get; }
        public BoundExpression Right { get; }

        public BoundAssignmentStatement(BoundExpression left, BoundExpression right)
            => (Left, Right) = (left, right);

        public override void Emit(ByteCodeBuilder code, BoundFunction parent)
        {
            if (Left.Type?.UnderlyingType is BasicType { TypeCode: BasicTypeCode.String } && Right.Type?.UnderlyingType is TextLabelType)
            {
                Right.EmitAddr(code);
            }
            else
            {
                Right.EmitLoad(code);
            }
            Left.EmitStore(code);
        }
    }

    public sealed class BoundIfStatement : BoundStatement
    {
        public BoundExpression Condition { get; }
        public IList<BoundStatement> Then { get; } = new List<BoundStatement>();
        public IList<BoundStatement> Else { get; } = new List<BoundStatement>();

        private readonly string id = Guid.NewGuid().ToString(); // unique ID used for labels

        public BoundIfStatement(BoundExpression condition)
            => Condition = condition;

        public override void Emit(ByteCodeBuilder code, BoundFunction parent)
        {
            var elseLabel = id + "-else";
            var exitLabel = id + "-exit";

            // TODO: use IEQ_JZ, INE_JZ, IGT_JZ, IGE_JZ, ILT_JZ and ILE_JZ when possible

            // if
            Condition.EmitLoad(code);
            code.EmitJumpIfZero(elseLabel);
            
            // then
            foreach (var stmt in Then)
            {
                stmt.Emit(code, parent);
            }
            if (Else.Count > 0)
            {
                // if the then block was executed and there is an else block, skip it
                code.EmitJump(exitLabel);
            }


            // else
            code.AddLabel(elseLabel);
            foreach (var stmt in Else)
            {
                stmt.Emit(code, parent);
            }

            code.AddLabel(exitLabel);
        }
    }

    public sealed class BoundWhileStatement : BoundStatement
    {
        public BoundExpression Condition { get; }
        public IList<BoundStatement> Block { get; } = new List<BoundStatement>();

        private readonly string id = Guid.NewGuid().ToString(); // unique ID used for labels

        public BoundWhileStatement(BoundExpression condition)
            => Condition = condition;

        public override void Emit(ByteCodeBuilder code, BoundFunction parent)
        {
            var beginLabel = id + "-begin";
            var exitLabel = id + "-exit";

            code.AddLabel(beginLabel);
            Condition.EmitLoad(code);
            code.EmitJumpIfZero(exitLabel);

            foreach (var stmt in Block)
            {
                stmt.Emit(code, parent);
            }
            code.EmitJump(beginLabel);

            code.AddLabel(exitLabel);
        }
    }

    public sealed class BoundSwitchStatement : BoundStatement
    {
        public BoundExpression Expression { get; }
        public IList<BoundSwitchCase> Cases { get; } = new List<BoundSwitchCase>();

        private readonly string id = Guid.NewGuid().ToString(); // unique ID used for labels

        public BoundSwitchStatement(BoundExpression expression)
            => Expression = expression;

        public override void Emit(ByteCodeBuilder code, BoundFunction parent)
        {
            var endLabel = id + "-end";

            Expression.EmitLoad(code);
            code.EmitSwitch(GetCases());

            var defaultCase = Cases.SingleOrDefault(c => c.IsDefault);
            if (defaultCase != null)
            {
                foreach (var stmt in defaultCase.Block)
                {
                    stmt.Emit(code, parent);
                }
            }
            code.EmitJump(endLabel);

            foreach (var c in Cases.Where(c => !c.IsDefault))
            {
                code.AddLabel(GetCaseLabel(c));
                foreach (var stmt in c.Block)
                {
                    stmt.Emit(code, parent);
                }
                code.EmitJump(endLabel);
            }

            code.AddLabel(endLabel);
        }

        private IEnumerable<(int Value, string Label)> GetCases()
        {
            foreach (var c in Cases.Where(c => !c.IsDefault))
            {
                yield return (c.Value!.Value, GetCaseLabel(c));
            }
        }

        private string GetCaseLabel(BoundSwitchCase c) => id + c.Id;
    }

    public sealed class BoundSwitchCase : BoundNode
    {
        public int? Value { get; }
        public IList<BoundStatement> Block { get; } = new List<BoundStatement>();

        public bool IsDefault => Value == null;

        public string Id { get; } = Guid.NewGuid().ToString();

        public BoundSwitchCase(int? value) => Value = value;
    }

    public sealed class BoundInvocationStatement : BoundStatement
    {
        public BoundExpression Callee { get; }
        public ImmutableArray<BoundExpression> Arguments { get; }

        public BoundInvocationStatement(BoundExpression callee, IEnumerable<BoundExpression> arguments)
        {
            if (!(callee is BoundInvalidExpression))
            {
                Debug.Assert(callee.Type is FunctionType);
            }

            Callee = callee;
            Arguments = arguments.ToImmutableArray();
        }

        public override void Emit(ByteCodeBuilder code, BoundFunction parent)
        {
            InvocationEmitter.Emit(code, Callee, Arguments, dropReturnValue: true);
        }
    }

    public sealed class BoundReturnStatement : BoundStatement
    {
        public BoundExpression? Expression { get; }

        public BoundReturnStatement(BoundExpression? expression)
        {
            Expression = expression;
        }

        public override void Emit(ByteCodeBuilder code, BoundFunction parent)
        {
            if (Expression != null)
            {
                if (parent.Function.Type.ReturnType is BasicType { TypeCode: BasicTypeCode.String } && Expression.Type?.UnderlyingType is TextLabelType)
                {
                    Expression.EmitAddr(code);
                }
                else
                {
                    Expression.EmitLoad(code);
                }
            }
            parent.EmitEpilogue(code);
        }
    }
}
