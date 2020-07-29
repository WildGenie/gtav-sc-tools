﻿#nullable enable
namespace ScTools.ScriptLang.Ast
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    public abstract class AstVisitor
    {
        public virtual void Visit(Node node) => node.Accept(this);
        public virtual void DefaultVisit(Node node) { }

        public virtual void VisitRoot(Root node) => DefaultVisit(node);

        public virtual void VisitScriptNameStatement(ScriptNameStatement node) => DefaultVisit(node);
        public virtual void VisitProcedureStatement(ProcedureStatement node) => DefaultVisit(node);
        public virtual void VisitStructStatement(StructStatement node) => DefaultVisit(node);
        public virtual void VisitStaticFieldStatement(StaticFieldStatement node) => DefaultVisit(node);

        public virtual void VisitStatementBlock(StatementBlock node) => DefaultVisit(node);
        public virtual void VisitVariableDeclarationStatement(VariableDeclarationStatement node) => DefaultVisit(node);
        public virtual void VisitAssignmentStatement(AssignmentStatement node) => DefaultVisit(node);
        public virtual void VisitIfStatement(IfStatement node) => DefaultVisit(node);
        public virtual void VisitWhileStatement(WhileStatement node) => DefaultVisit(node);
        public virtual void VisitCallStatement(CallStatement node) => DefaultVisit(node);

        public virtual void VisitParenthesizedExpression(ParenthesizedExpression node) => DefaultVisit(node);
        public virtual void VisitNotExpression(NotExpression node) => DefaultVisit(node);
        public virtual void VisitBinaryExpression(BinaryExpression node) => DefaultVisit(node);
        public virtual void VisitAggregateExpression(AggregateExpression node) => DefaultVisit(node);
        public virtual void VisitIdentifierExpression(IdentifierExpression node) => DefaultVisit(node);
        public virtual void VisitMemberAccessExpression(MemberAccessExpression node) => DefaultVisit(node);
        public virtual void VisitArrayAccessExpression(ArrayAccessExpression node) => DefaultVisit(node);
        public virtual void VisitCallExpression(CallExpression node) => DefaultVisit(node);
        public virtual void VisitLiteralExpression(LiteralExpression node) => DefaultVisit(node);

        public virtual void VisitIdentifier(Identifier node) => DefaultVisit(node);
        public virtual void VisitArrayIndexer(ArrayIndexer node) => DefaultVisit(node);
        public virtual void VisitType(Type node) => DefaultVisit(node);
        public virtual void VisitVariable(Variable node) => DefaultVisit(node);
        public virtual void VisitParameterList(ParameterList node) => DefaultVisit(node);
        public virtual void VisitProcedureCall(ProcedureCall node) => DefaultVisit(node);
    }

    public abstract class AstVisitor<TResult>
    {
        [return: MaybeNull] public virtual TResult Visit(Node node) => node.Accept(this);
        [return: MaybeNull] public virtual TResult DefaultVisit(Node node) => default;

        [return: MaybeNull] public virtual TResult VisitRoot(Root node) => DefaultVisit(node);
        
        [return: MaybeNull] public virtual TResult VisitScriptNameStatement(ScriptNameStatement node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitProcedureStatement(ProcedureStatement node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitStructStatement(StructStatement node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitStaticFieldStatement(StaticFieldStatement node) => DefaultVisit(node);

        [return: MaybeNull] public virtual TResult VisitStatementBlock(StatementBlock node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitVariableDeclarationStatement(VariableDeclarationStatement node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitAssignmentStatement(AssignmentStatement node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitIfStatement(IfStatement node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitWhileStatement(WhileStatement node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitCallStatement(CallStatement node) => DefaultVisit(node);

        [return: MaybeNull] public virtual TResult VisitParenthesizedExpression(ParenthesizedExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitNotExpression(NotExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitBinaryExpression(BinaryExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitAggregateExpression(AggregateExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitIdentifierExpression(IdentifierExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitMemberAccessExpression(MemberAccessExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitArrayAccessExpression(ArrayAccessExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitCallExpression(CallExpression node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitLiteralExpression(LiteralExpression node) => DefaultVisit(node);

        [return: MaybeNull] public virtual TResult VisitIdentifier(Identifier node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitArrayIndexer(ArrayIndexer node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitType(Type node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitVariable(Variable node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitParameterList(ParameterList node) => DefaultVisit(node);
        [return: MaybeNull] public virtual TResult VisitProcedureCall(ProcedureCall node) => DefaultVisit(node);
    }

    public static class NodeVisitorExtensions
    {
        public static void Accept(this Node node, AstVisitor visitor)
        {
            switch (node)
            {
                case Root n: visitor.VisitRoot(n); break;
                
                case ScriptNameStatement n: visitor.VisitScriptNameStatement(n); break;
                case ProcedureStatement n: visitor.VisitProcedureStatement(n); break;
                case StructStatement n: visitor.VisitStructStatement(n); break;
                case StaticFieldStatement n: visitor.VisitStaticFieldStatement(n); break;
                
                case StatementBlock n: visitor.VisitStatementBlock(n); break;
                case VariableDeclarationStatement n: visitor.VisitVariableDeclarationStatement(n); break;
                case AssignmentStatement n: visitor.VisitAssignmentStatement(n); break;
                case IfStatement n: visitor.VisitIfStatement(n); break;
                case WhileStatement n: visitor.VisitWhileStatement(n); break;
                case CallStatement n: visitor.VisitCallStatement(n); break;
                
                case ParenthesizedExpression n: visitor.VisitParenthesizedExpression(n); break;
                case NotExpression n: visitor.VisitNotExpression(n); break;
                case BinaryExpression n: visitor.VisitBinaryExpression(n); break;
                case AggregateExpression n: visitor.VisitAggregateExpression(n); break;
                case IdentifierExpression n: visitor.VisitIdentifierExpression(n); break;
                case MemberAccessExpression n: visitor.VisitMemberAccessExpression(n); break;
                case ArrayAccessExpression n: visitor.VisitArrayAccessExpression(n); break;
                case CallExpression n: visitor.VisitCallExpression(n); break;
                case LiteralExpression n: visitor.VisitLiteralExpression(n); break;

                case Identifier n: visitor.VisitIdentifier(n); break;
                case ArrayIndexer n: visitor.VisitArrayIndexer(n); break;
                case Type n: visitor.VisitType(n); break;
                case Variable n: visitor.VisitVariable(n); break;
                case ParameterList n: visitor.VisitParameterList(n); break;
                case ProcedureCall n: visitor.VisitProcedureCall(n); break;

                default: throw new NotImplementedException();
            }
        }

        [return: MaybeNull]
        public static TResult Accept<TResult>(this Node node, AstVisitor<TResult> visitor) => node switch
        {
            Root n => visitor.VisitRoot(n),

            ScriptNameStatement n => visitor.VisitScriptNameStatement(n),
            ProcedureStatement n => visitor.VisitProcedureStatement(n),
            StructStatement n => visitor.VisitStructStatement(n),
            StaticFieldStatement n => visitor.VisitStaticFieldStatement(n),

            StatementBlock n => visitor.VisitStatementBlock(n),
            VariableDeclarationStatement n => visitor.VisitVariableDeclarationStatement(n),
            AssignmentStatement n => visitor.VisitAssignmentStatement(n),
            IfStatement n => visitor.VisitIfStatement(n),
            WhileStatement n => visitor.VisitWhileStatement(n),
            CallStatement n => visitor.VisitCallStatement(n),

            ParenthesizedExpression n => visitor.VisitParenthesizedExpression(n),
            NotExpression n => visitor.VisitNotExpression(n),
            BinaryExpression n => visitor.VisitBinaryExpression(n),
            AggregateExpression n => visitor.VisitAggregateExpression(n),
            IdentifierExpression n => visitor.VisitIdentifierExpression(n),
            MemberAccessExpression n => visitor.VisitMemberAccessExpression(n),
            ArrayAccessExpression n => visitor.VisitArrayAccessExpression(n),
            CallExpression n => visitor.VisitCallExpression(n),
            LiteralExpression n => visitor.VisitLiteralExpression(n),

            Identifier n => visitor.VisitIdentifier(n),
            ArrayIndexer n => visitor.VisitArrayIndexer(n),
            Type n =>  visitor.VisitType(n),
            Variable n => visitor.VisitVariable(n),
            ParameterList n => visitor.VisitParameterList(n),
            ProcedureCall n => visitor.VisitProcedureCall(n),

            _ => throw new NotImplementedException()
        };
    }
}
