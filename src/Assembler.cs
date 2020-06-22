﻿namespace ScTools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using CodeWalker.GameFiles;
    using ScTools.GameFiles;

    internal partial class Assembler
    {
        private const char DirectiveChar = '$';
        private const char CommentChar = ';';

        private Script sc;
        private readonly List<ulong> nativeHashes = new List<ulong>();
        private readonly HashSet<string> labels = new HashSet<string>();
        private string lastLabel = null;
        private CodeBuilder code = null;

        public Script Assemble(FileInfo inputFile)
        {
            const string DefaultName = "unknown";

            Script sc = new Script()
            {
                Hash = 0, // TODO: how is this hash calculated?
                ArgsCount = 0,
                StaticsCount = 0,
                GlobalsLengthAndBlock = 0,
                NativesCount = 0,
                Name = DefaultName,
                NameHash = DefaultName.ToHash(),
                StringsLength = 0,
            };

            this.sc = sc;
            Parse(inputFile);
            this.sc = null;
            return sc;
        }

        private void Parse(FileInfo inputFile)
        {
            nativeHashes.Clear();
            labels.Clear();
            lastLabel = null;
            code = new CodeBuilder();

            using TextReader input = new StreamReader(inputFile.OpenRead());
            string line = null;

            while ((line = input.ReadLine()) != null)
            {
                ParseLine(line);
            }

            sc.CodePages = new ScriptPageArray<byte>
            {
                Items = code.ToPages(out uint codeLength),
            };
            sc.CodeLength = codeLength;

            static ulong RotateHash(ulong hash, int index, uint codeLength)
            {
                byte rotate = (byte)(((uint)index + codeLength) & 0x3F);
                return hash >> rotate | hash << (64 - rotate);
            }

            sc.Natives = nativeHashes.Select((h, i) => RotateHash(h, i, codeLength)).ToArray();
            sc.NativesCount = (uint)sc.Natives.Length;

            Debug.Assert(sc.Natives.Select((h, i) => sc.NativeHash(i) == nativeHashes[i]).All(b => b));

            code = null;
        }

        private void ParseLine(string line)
        {
            ReadOnlySpan<char> l = line;
            l = l.Trim();

            if (l.Length == 0)
            {
                return;
            }

            var tokens = Tokenize(line).GetEnumerator();
            if (tokens.MoveNext())
            {
                bool parsed = ParseDirective(tokens) ||
                              ParseLabel(tokens) ||
                              ParseInstruction(tokens);

                if (!parsed)
                {
                    throw new AssemblerSyntaxException($"Unknown syntax '{line}'");
                }
            }
        }

        private bool ParseLabel(TokenEnumerator tokens)
        {
            var token = tokens.Current;

            if (token[^1] == ':') // label
            {
                if (lastLabel != null)
                {
                    throw new AssemblerSyntaxException($"Found label '{token.ToString()}' right after label '{lastLabel}'");
                }

                if (tokens.MoveNext())
                {
                    throw new AssemblerSyntaxException($"Unknown token '{tokens.Current.ToString()}' after label '{token.ToString()}'");
                }

                lastLabel = token[0..^1].ToString(); // remove ':'
                if (!labels.Add(lastLabel))
                {
                    throw new AssemblerSyntaxException($"Label '{token.ToString()}' is repeated");
                }

                return true;
            }

            return false;
        }

        private bool ParseDirective(TokenEnumerator tokens)
        {
            var directive = tokens.Current;

            if (directive[0] != DirectiveChar)
            {
                return false;
            }

            directive = directive.Slice(1); // remove DirectiveChar

            int dirIndex = Directive.Find(directive.ToHash());
            if (dirIndex != -1)
            {
                ref Directive dir = ref Directives[dirIndex];
                dir.Callback(dir, tokens, this);
                return true;
            }
            else
            {
                throw new AssemblerSyntaxException($"Unknown directive '{DirectiveChar}{directive.ToString()}'");
            }
        }

        private bool ParseInstruction(TokenEnumerator tokens)
        {
            uint mnemonic = tokens.Current.ToHash();
            int instIndex = Inst.Find(mnemonic);
            if (instIndex != -1)
            {
                ref Inst inst = ref Instructions[instIndex];
                inst.Builder(inst, tokens, lastLabel, code);
                lastLabel = null;
                return true;
            }

            return false;
        }

        private void SetName(string name)
        {
            Debug.Assert(sc != null);

            sc.Name = name ?? throw new ArgumentNullException(nameof(name));
            sc.NameHash = name.ToHash();
        }

        private void SetStaticsCount(uint count)
        {
            Debug.Assert(sc != null);

            sc.Statics = count == 0 ? null : new ScriptValue[count];
            sc.StaticsCount = count;
        }

        private void SetStaticValue(uint staticIndex, int value)
        {
            Debug.Assert(sc != null);
            
            if (sc.Statics == null || staticIndex >= sc.Statics.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(staticIndex));
            }

            sc.Statics[staticIndex].AsInt32 = value;
        }

        private void SetStaticValue(uint staticIndex, float value)
        {
            Debug.Assert(sc != null);

            if (sc.Statics == null || staticIndex >= sc.Statics.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(staticIndex));
            }

            sc.Statics[staticIndex].AsFloat = value;
        }

        private void AddNative(ulong hash)
        {
            Debug.Assert(sc != null);

            if (nativeHashes.Contains(hash))
            {
                throw new AssemblerSyntaxException($"Native hash {hash:X16} is repeated");
            }

            nativeHashes.Add(hash);
        }

        private class CodeBuilder
        {
            private readonly List<byte[]> pages = new List<byte[]>();
            private readonly List<(string Label, uint IP)> labels = new List<(string, uint)>();
            private readonly List<(string TargetLabel, uint IP)> targetLabels = new List<(string, uint)>();
            private readonly List<(string TargetLabel, uint IP)> relativeTargetLabels = new List<(string, uint)>();
            private uint length = 0;
            private readonly List<byte> buffer = new List<byte>();

            private bool inInstruction = false;

            public void BeginInstruction(string label)
            {
                Debug.Assert(!inInstruction);

                buffer.Clear();
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add((label, length));
                }
                inInstruction = true;
            }

            public void Add(byte v)
            {
                Debug.Assert(inInstruction);

                buffer.Add(v);

                Debug.Assert(buffer.Count < Script.MaxPageLength / 2, "instruction too long");
            }

            public void Add(ushort v)
            {
                Debug.Assert(inInstruction);

                buffer.Add((byte)(v & 0xFF));
                buffer.Add((byte)(v >> 8));

                Debug.Assert(buffer.Count < Script.MaxPageLength / 2, "instruction too long");
            }

            public void Add(short v)
            {
                Debug.Assert(inInstruction);

                buffer.Add((byte)(v & 0xFF));
                buffer.Add((byte)(v >> 8));

                Debug.Assert(buffer.Count < Script.MaxPageLength / 2, "instruction too long");
            }

            public void Add(uint v)
            {
                Debug.Assert(inInstruction);

                buffer.Add((byte)(v & 0xFF));
                buffer.Add((byte)((v >> 8) & 0xFF));
                buffer.Add((byte)((v >> 16) & 0xFF));
                buffer.Add((byte)(v >> 24));

                Debug.Assert(buffer.Count < Script.MaxPageLength / 2, "instruction too long");
            }

            public void AddU24(uint v)
            {
                Debug.Assert(inInstruction);

                buffer.Add((byte)(v & 0xFF));
                buffer.Add((byte)((v >> 8) & 0xFF));
                buffer.Add((byte)((v >> 16) & 0xFF));

                Debug.Assert(buffer.Count < Script.MaxPageLength / 2, "instruction too long");
            }

            public unsafe void Add(float v) => Add(*(uint*)&v);

            public void AddTarget(string label)
            {
                Debug.Assert(inInstruction);

                if (string.IsNullOrWhiteSpace(label))
                {
                    throw new ArgumentException("null or empty label", nameof(label));
                }

                // TODO: what happens if this is done in the page boundary where the instruction doesn't fit?
                // the IP value may no longer match the instruction position and FixupTargetLabels will write the address in the wrong position
                targetLabels.Add((label, length + (uint)buffer.Count));
                buffer.Add(0);
                buffer.Add(0);
                buffer.Add(0);

                Debug.Assert(buffer.Count < Script.MaxPageLength / 2, "instruction too long");
            }

            public void AddRelativeTarget(string label)
            {
                Debug.Assert(inInstruction);

                if (string.IsNullOrWhiteSpace(label))
                {
                    throw new ArgumentException("null or empty label", nameof(label));
                }

                // TODO: what happens if this is done in the page boundary where the instruction doesn't fit?
                // the IP value may no longer match the instruction position and FixupRelativeTargetLabels will write the address in the wrong position
                relativeTargetLabels.Add((label, length + (uint)buffer.Count));
                buffer.Add(0);
                buffer.Add(0);

                Debug.Assert(buffer.Count < Script.MaxPageLength / 2, "instruction too long");
            }

            public void EndInstruction()
            {
                Debug.Assert(inInstruction);

                uint pageIndex = length >> 14;
                if (pageIndex >= pages.Count)
                {
                    AddPage();
                }

                uint offset = length & 0x3FFF;
                byte[] page = pages[(int)pageIndex];

                if (offset + buffer.Count > page.Length)
                {
                    // the instruction doesn't fit in the current page, skip until the next one (page is already zeroed out/filled with NOPs)
                    length += (uint)page.Length - offset;
                    pageIndex = length >> 14;
                    offset = length & 0x3FFF;
                    AddPage();
                    page = pages[(int)pageIndex];
                }

                buffer.CopyTo(page, (int)offset);
                length += (uint)buffer.Count;

                inInstruction = false;
            }

            private void FixupTargetLabels()
            {
                foreach (var (targetLabel, targetIP) in targetLabels)
                {
                    var (label, ip) = labels.Find(l => l.Label == targetLabel);
                    if (label == null)
                    {
                        throw new AssemblerSyntaxException($"Unknown label '{targetLabel}'");
                    }

                    byte[] targetPage = pages[(int)(targetIP >> 14)];
                    uint targetOffset = targetIP & 0x3FFF;

                    targetPage[targetOffset + 0] = (byte)(ip & 0xFF);
                    targetPage[targetOffset + 1] = (byte)((ip >> 8) & 0xFF);
                    targetPage[targetOffset + 2] = (byte)(ip >> 16);
                }

                targetLabels.Clear();
            }

            private void FixupRelativeTargetLabels()
            {
                foreach (var (targetLabel, targetIP) in relativeTargetLabels)
                {
                    var (label, ip) = labels.Find(l => l.Label == targetLabel);
                    if (label == null)
                    {
                        throw new AssemblerSyntaxException($"Unknown label '{targetLabel}'");
                    }

                    byte[] targetPage = pages[(int)(targetIP >> 14)];
                    uint targetOffset = targetIP & 0x3FFF;

                    short relIP = (short)((int)ip - (int)(targetIP + 2));
                    targetPage[targetOffset + 0] = (byte)(relIP & 0xFF);
                    targetPage[targetOffset + 1] = (byte)(relIP >> 8);
                }

                relativeTargetLabels.Clear();
            }

            public ScriptPage<byte>[] ToPages(out uint codeLength)
            {
                FixupTargetLabels();
                FixupRelativeTargetLabels();

                var p = new ScriptPage<byte>[pages.Count];
                for (int i = 0; i < pages.Count - 1; i++)
                {
                    p[i] = new ScriptPage<byte> { Data = NewPage() };
                    pages[i].CopyTo(p[i].Data, 0);
                }

                p[^1] = new ScriptPage<byte> { Data = NewPage(length & 0x3FFF) };
                Array.Copy(pages[^1], p[^1].Data, p[^1].Data.Length);

                codeLength = length;
                return p;
            }

            private void AddPage() => pages.Add(NewPage());
            private byte[] NewPage(uint size = Script.MaxPageLength) => new byte[size];
        }

        private static TokenEnumerable Tokenize(ReadOnlySpan<char> line) => new TokenEnumerable(line);

        private ref struct TokenEnumerable
        {
            public ReadOnlySpan<char> Line { get; }

            public TokenEnumerable(ReadOnlySpan<char> line) => Line = line;

            public TokenEnumerator GetEnumerator() => new TokenEnumerator(Line);
        }

        private ref struct TokenEnumerator
        {
            private ReadOnlySpan<char> line;
            private int currentLength;

            public TokenEnumerator(ReadOnlySpan<char> line) : this() => this.line = line;

            public bool MoveNext()
            {
                if (currentLength >= line.Length)
                {
                    return false;
                }

                line = line.Slice(currentLength);
                if (line.Length > 0)
                {
                    // skip until we find non-whitespace character
                    int i = 0;
                    while (i < line.Length && char.IsWhiteSpace(line[i])) { i++; }

                    line = line.Slice(i);

                    if (line.Length > 0 && line[0] == CommentChar)
                    {
                        // found a comment, ignore the rest of the line;
                        return false;
                    }

                    // continue until we find a whitespace character
                    currentLength = 0;
                    while (currentLength < line.Length && !char.IsWhiteSpace(line[currentLength])) { currentLength++; }

                    return currentLength > 0;
                }

                return false;
            }

            public ReadOnlySpan<char> Current => line.Slice(0, currentLength);
        }
    }

    [Serializable]
    public class AssemblerSyntaxException : Exception
    {
        public AssemblerSyntaxException() { }
        public AssemblerSyntaxException(string message) : base(message) { }
        public AssemblerSyntaxException(string message, Exception inner) : base(message, inner) { }
        protected AssemblerSyntaxException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
