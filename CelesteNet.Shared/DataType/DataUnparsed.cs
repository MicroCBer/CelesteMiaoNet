﻿using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.CelesteNet.DataTypes {
    public class DataUnparsed : DataType<DataUnparsed> {

        static DataUnparsed() {
            DataID = "unparsed";
            DataFlags = DataFlags.ForceForward;
        }

        public override bool IsValid => false;
        public override bool IsSendable => false;

        public string InnerID;
        public DataFlags InnerFlags;
        public byte[] InnerData;

        public override void Read(BinaryReader reader) {
            InnerID = reader.ReadNullTerminatedString();
            InnerFlags = (DataFlags) reader.ReadUInt16();
            InnerData = reader.ReadBytes(reader.ReadUInt16());
        }

        public override void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(InnerID);
            writer.Write((ushort) InnerFlags);
            writer.Write(InnerData);
        }

        public override DataUnparsed CloneT()
            => new DataUnparsed {
                InnerID = InnerID,
                InnerFlags = InnerFlags,
                InnerData = InnerData.ToArray(),
            };

    }
}