﻿using Celeste.Mod.CelesteNet.DataTypes;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectFactories;

namespace Celeste.Mod.CelesteNet {
    public class CelesteNetBinaryWriter : BinaryWriter {

        public readonly DataContext Data;

        public OptMap<string>? Strings;
        public OptMap<Type>? SlimMap;

        protected ConcurrentStack<(long pos, int size)> SizeDummyStack;

        public CelesteNetBinaryWriter(DataContext ctx, OptMap<string>? strings, OptMap<Type>? slimMap, Stream output)
            : base(output, CelesteNetUtils.UTF8NoBOM) {
            Data = ctx;
            Strings = strings;
            SlimMap = slimMap;
            SizeDummyStack = new ConcurrentStack<(long, int)>();
        }

        public CelesteNetBinaryWriter(DataContext ctx, OptMap<string>? strings, OptMap<Type>? slimMap, Stream output, bool leaveOpen)
            : base(output, CelesteNetUtils.UTF8NoBOM, leaveOpen) {
            Data = ctx;
            Strings = strings;
            SlimMap = slimMap;
            SizeDummyStack = new ConcurrentStack<(long, int)>();
        }

        public virtual void WriteSizeDummy(int size) {
            Flush();
            long pos = BaseStream.Position;

            if (size == 1)
                Write((byte) 0);
            else if (size == 2)
                Write((ushort) 0);
            else if (size == 4)
                Write((uint) 0);
            else
                throw new ArgumentException($"Invalid size dummy size {size}");

            SizeDummyStack.Push((pos, size));
        }

        public virtual void UpdateSizeDummy() {
            if (!SizeDummyStack.TryPop(out var dummy))
                throw new InvalidOperationException("No size dummy on the stack");

            Flush();
            long end = BaseStream.Position;
            long length = end - (dummy.pos + dummy.size);

            BaseStream.Seek(dummy.pos, SeekOrigin.Begin);

            if (dummy.size == 1)
                Write((byte) length);
            else if (dummy.size == 2)
                Write((ushort) length);
            else if (dummy.size == 4)
                Write((uint) length);

            Flush();
            BaseStream.Seek(end, SeekOrigin.Begin);
        }

        public new void Write7BitEncodedInt(int value)
            => base.Write7BitEncodedInt(value);

        public virtual void Write(Vector2 value) {
            Write(value.X);
            Write(value.Y);
        }

        public virtual void Write(Color value) {
            Write(value.R);
            Write(value.G);
            Write(value.B);
            Write(value.A);
        }

        public virtual void WriteNoA(Color value) {
            Write(value.R);
            Write(value.G);
            Write(value.B);
        }

        public virtual void Write(DateTime value) {
            Write(value.ToBinary());
        }

        public virtual unsafe void WriteNetString(string? text) {
            if (text != null) {
                if (text.Length > 4096)
                    throw new Exception("String too long.");
                fixed (char* src = text) {
                    char* ptr = src;
                    for (int i = text.Length; i > 0; i--) {
                        Write(*ptr++);
                    }
                }
            }
            Write('\0');
        }

        public virtual void WriteNetMappedString(string? text) {
            if (Strings == null || !Strings.TryMap(text, out int id)) {
                WriteNetString(text);
                return;
            }

            Write((byte) 0xFF);
            Write7BitEncodedInt(id);
        }

        public virtual bool TryGetSlimID(Type type, out int slimID) {
            slimID = -1;
            return SlimMap != null && SlimMap.TryMap(type, out slimID);
        }

        public void WriteRef<T>(T? data) where T : DataType<T>
            => Write7BitEncodedInt(unchecked((int) ((data ?? throw new Exception($"Expected {Data.DataTypeToID[typeof(T)]} to write, got null")).Get<MetaRef>(Data) ?? uint.MaxValue)));

        public void WriteOptRef<T>(T? data) where T : DataType<T>
            => Write7BitEncodedInt(unchecked((int) (data?.GetOpt<MetaRef>(Data) ?? uint.MaxValue)));

    }
}
