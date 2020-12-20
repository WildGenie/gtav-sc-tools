﻿#nullable enable
namespace ScTools.ScriptLang.Semantics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ScTools.ScriptLang.Semantics.Symbols;

    public abstract class Type : IEquatable<Type>
    {
        public abstract int SizeOf { get; }

        public abstract Type Clone();
        public abstract Type? Resolve(SymbolTable symbols);
        public virtual bool HasField(string name) { return false; }

        public abstract bool Equals(Type? other);

        protected abstract int DoGetHashCode();
        protected abstract string DoToString();

        public override bool Equals(object? obj)
            => obj is Type t && Equals(t);

        public override int GetHashCode() => DoGetHashCode();
        public override string ToString() => DoToString();

        public static bool operator ==(Type? lhs, Type? rhs) => ReferenceEquals(lhs, rhs) || (lhs?.Equals(rhs) ?? false);
        public static bool operator !=(Type? lhs, Type? rhs) => !(lhs == rhs);

        /// <summary>
        /// Checks whether <paramref name="dest"/> and <paramref name="src"/> are the same type;
        /// or, if both are <see cref="StructType"/>, whether <paramref name="src"/> is an aggregate type with the same field layout as <paramref name="dest"/>.
        /// </summary>
        public bool IsAssignableFrom(Type src, bool considerReferences)
        {
            var dest = this;
            bool assignable = dest == src;

            if (!assignable)
            {
                if (dest is StructType destStructType && src is StructType srcStructType)
                {
                    bool srcIsAggregate = srcStructType.Name == null;
                    assignable = srcIsAggregate && srcStructType.HasSameFieldLayout(destStructType);
                }
                else if (considerReferences)
                {
                    assignable = (dest is RefType destRefType && destRefType.ElementType.IsAssignableFrom(src, considerReferences: false)) ||
                                 (src is RefType srcRefType && dest.IsAssignableFrom(srcRefType.ElementType, considerReferences: false));
                }
            }

            return assignable;
        }

        /// <summary>
        /// Gets this <see cref="Type"/>, except for <see cref="RefType"/>s, in which it returns its <see cref="RefType.ElementType"/>.
        /// </summary>
        public Type UnderlyingType => this is RefType refType ? refType.ElementType : this;
    }

    public sealed class RefType : Type
    {
        public override int SizeOf => 1;
        public Type ElementType { get; set; }

        public RefType(Type elementType)
            => ElementType = elementType is RefType ? throw new ArgumentException("References to references are not allowed") :
                                                      elementType;

        public override RefType Clone() => new RefType(ElementType);

        public override RefType? Resolve(SymbolTable symbols)
        {
            var resolvedElementType = ElementType.Resolve(symbols);
            if (resolvedElementType == null)
            {
                return null;
            }

            return new RefType(resolvedElementType);
        }

        public override bool Equals(Type? other)
            => other is RefType r && r.ElementType == ElementType;

        protected override int DoGetHashCode()
            => HashCode.Combine(ElementType);

        protected override string DoToString() => $"{ElementType}&";
    }

    public sealed class BasicType : Type
    {
        public BasicTypeCode TypeCode { get; }
        public override int SizeOf => 1;

        public BasicType(BasicTypeCode typeCode) => TypeCode = typeCode;

        public override BasicType Clone() => new BasicType(TypeCode);
        public override BasicType? Resolve(SymbolTable symbols) => this;

        public override bool Equals(Type? other)
            => other is BasicType b && b.TypeCode == TypeCode;

        protected override int DoGetHashCode()
            => HashCode.Combine(TypeCode);

        protected override string DoToString() => $"{TypeCode.ToString().ToUpper()}";
    }

    public enum BasicTypeCode
    {
        Int,
        Float,
        Bool,
        String,
    }

    public sealed class StructType : Type
    {
        public string? Name { get; set; }
        public IList<Field> Fields { get; set; }
        public override int SizeOf => Fields.Sum(f => f.Type.SizeOf);

        public StructType(string? name, IEnumerable<Field> fields)
            => (Name, Fields) = (name, new List<Field>(fields));

        public StructType(string? name, params Field[] fields) : this(name, (IEnumerable<Field>)fields) { }

        public override StructType Clone() => new StructType(Name, Fields);

        public override StructType? Resolve(SymbolTable symbols)
        {
            var resolvedFields = Fields.Select(f => (Type: f.Type.Resolve(symbols), f.Name)).ToArray();
            if (resolvedFields.Any(f => f.Type == null))
            {
                return null;
            }

            return new StructType(Name, resolvedFields.Select(f => new Field(f.Type!, f.Name)));
        }

        public override bool HasField(string name)
        {
            foreach (var f in Fields)
            {
                if (f.Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        public Type TypeOfField(string name)
        {
            foreach (var f in Fields)
            {
                if (f.Name == name)
                {
                    return f.Type;
                }
            }

            throw new ArgumentException($"No field with name '{name}' exists in struct '{Name}'");
        }

        public int OffsetOfField(string name)
        {
            int offset = 0;
            foreach (var f in Fields)
            {
                if (f.Name == name)
                {
                    return offset;
                }
                else
                {
                    offset += f.Type.SizeOf;
                }
            }

            throw new ArgumentException($"No field with name '{name}' exists in struct '{Name}'");
        }

        public bool Equals(Type? other, bool ignoreNames)
        {
            if (!(other is StructType s))
            {
                return false;
            }

            if (s.Name != Name)
            {
                return false;
            }

            if (s.Fields.Count != Fields.Count)
            {
                return false;
            }

            for (int i = 0; i < Fields.Count; i++)
            {
                if (s.Fields[i] != Fields[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(Type? other)
        {
            if (!(other is StructType s))
            {
                return false;
            }

            if (s.Name != Name)
            {
                return false;
            }

            if (s.Fields.Count != Fields.Count)
            {
                return false;
            }

            for (int i = 0; i < Fields.Count; i++)
            {
                if (s.Fields[i] != Fields[i])
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasSameFieldLayout(StructType other)
        {
            if (other.Fields.Count != Fields.Count)
            {
                return false;
            }

            for (int i = 0; i < Fields.Count; i++)
            {
                if (!SameTypeIgnoreFieldNames(other.Fields[i].Type, Fields[i].Type))
                {
                    return false;
                }
            }

            return true;

            static bool SameTypeIgnoreFieldNames(Type left, Type right)
            {
                if (left == right)
                {
                    return true;
                }

                if (left is StructType leftStruct && right is StructType rightStruct)
                {
                    return leftStruct.HasSameFieldLayout(rightStruct);
                }

                return false;
            }
        }

        protected override int DoGetHashCode()
        {
            HashCode h = default;
            foreach (var f in Fields)
            {
                h.Add(f);
            }
            return h.ToHashCode();
        }

        protected override string DoToString() => Name ?? "<<unknown>>";

        public static Type NewAggregate(IEnumerable<Type> fieldTypes)
            => new StructType(null, fieldTypes.Select((t, i) => new Field(t, $"_item{i}")));
    }

    public readonly struct Field : IEquatable<Field>
    {
        public Type Type { get; }
        public string Name { get; }

        public Field(Type type, string name) => (Type, Name) = (type, name);

        public bool Equals(Field other)
            => other.Type == Type && other.Name == Name;

        public override bool Equals(object? obj)
            => obj is Field f && Equals(f);

        public override int GetHashCode()
            => HashCode.Combine(Type, Name);

        public static bool operator ==(Field lhs, Field rhs) => lhs.Equals(rhs);
        public static bool operator !=(Field lhs, Field rhs) => !(lhs == rhs);
    }

    public sealed class FunctionType : Type
    {
        public Type? ReturnType { get; set; }
        public IList<(Type Type, string? Name)> Parameters { get; set; }
        public override int SizeOf => 1;

        public FunctionType(Type? returnType, IEnumerable<Type> parameters)
            : this(returnType, parameters.Select(t => (t, (string?)null)))
        { }

        public FunctionType(Type? returnType, IEnumerable<(Type, string?)> parameters)
            => (ReturnType, Parameters) = (returnType, new List<(Type, string?)>(parameters));

        public override FunctionType Clone() => new FunctionType(ReturnType, Parameters);

        public override FunctionType? Resolve(SymbolTable symbols)
        {
            var resolvedReturnType = ReturnType?.Resolve(symbols);
            if (resolvedReturnType == null && ReturnType != null)
            {
                return null;
            }

            var resolvedParameters = Parameters.Select(p => (Type: p.Type.Resolve(symbols), p.Name)).ToArray();
            if (resolvedParameters.Any(p => p.Type == null))
            {
                return null;
            }

            return new FunctionType(resolvedReturnType, resolvedParameters!);
        }

        public override bool Equals(Type? other)
        {
            if (other is not FunctionType f)
            {
                return false;
            }

            if (f.ReturnType != ReturnType)
            {
                return false;
            }

            if (f.Parameters.Count != Parameters.Count)
            {
                return false;
            }

            for (int i = 0; i < Parameters.Count; i++)
            {
                // note: two FunctionTypes with different parameter names should be considered equal, so only check the types
                if (f.Parameters[i].Type != Parameters[i].Type)
                {
                    return false;
                }
            }

            return true;
        }

        protected override int DoGetHashCode()
        {
            HashCode h = default;
            h.Add(ReturnType);
            foreach (var p in Parameters)
            {
                h.Add(p.Type);
            }
            return h.ToHashCode();
        }

        protected override string DoToString() => "";
    }

    public sealed class ArrayType : Type
    {
        public const string LengthFieldName = "length";

        public override int SizeOf => 1 + (ItemType.SizeOf * Length);
        public Type ItemType { get; set; }
        public int Length { get; set; }

        public ArrayType(Type itemType, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length is negative");
            }

            if (itemType is RefType)
            {
                throw new ArgumentException("Arrays of references are not allowed");
            }

            ItemType = itemType;
            Length = length;
        }

        public override ArrayType Clone() => new ArrayType(ItemType, Length);

        public override ArrayType? Resolve(SymbolTable symbols)
        {
            var resolvedItemType = ItemType.Resolve(symbols);
            if (resolvedItemType == null)
            {
                return null;
            }

            return new ArrayType(resolvedItemType, Length);
        }

        public override bool HasField(string name) => name == LengthFieldName;

        public override bool Equals(Type? other)
            => other is ArrayType arr && arr.Length == Length && arr.ItemType == ItemType;

        protected override int DoGetHashCode() => HashCode.Combine(ItemType, Length);
        protected override string DoToString() => $"{ItemType}[{Length}]";
    }

    public sealed class UnresolvedType : Type
    {
        public string TypeName { get; }
        public override int SizeOf => throw new InvalidOperationException("Unresolved type");

        public UnresolvedType(string typeName)
            => TypeName = typeName;

        public override UnresolvedType Clone() => new UnresolvedType(TypeName);

        public override Type? Resolve(SymbolTable symbols)
        {
            var symbol = symbols.Lookup(TypeName);
            return (symbol as TypeSymbol)?.Type;
        }

        public override bool Equals(Type? other)
            => other is UnresolvedType t && t.TypeName == TypeName;

        protected override int DoGetHashCode()
            => HashCode.Combine(TypeName);

        protected override string DoToString() => TypeName;
    }
}
