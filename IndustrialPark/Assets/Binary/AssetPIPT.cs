﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using HipHopFile;

namespace IndustrialPark
{
    public class EntryPIPT
    {
        [Category("PIPT Entry")]
        public AssetID ModelAssetID { get; set; }
        [Category("PIPT Entry")]
        public int MeshIndex { get; set; }
        [Category("PIPT Entry"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte RelatedToVisibility { get; set; }
        [Category("PIPT Entry"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte Culling { get; set; }
        [Category("PIPT Entry"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte DestinationSourceBlend { get; set; }
        [Category("PIPT Entry"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte OtherFlags { get; set; }
        [Category("PIPT Entry (Movie only)"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte Unknown0C { get; set; }
        [Category("PIPT Entry (Movie only)"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte Unknown0D { get; set; }
        [Category("PIPT Entry (Movie only)"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte Unknown0E { get; set; }
        [Category("PIPT Entry (Movie only)"), TypeConverter(typeof(HexByteTypeConverter))]
        public byte Unknown0F { get; set; }

        public static int SizeOfStruct(Game game) => game == Game.Incredibles ? 16 : 12;

        public EntryPIPT()
        {
            ModelAssetID = 0;
        }
        
        public override string ToString()
        {
            return $"{Program.MainForm.GetAssetNameFromID(ModelAssetID)} - {MeshIndex}";
        }
    }

    public class AssetPIPT : Asset
    {
        public AssetPIPT(Section_AHDR AHDR, Game game, Platform platform) : base(AHDR, game, platform) { }

        public override bool HasReference(uint assetID)
        {
            foreach (EntryPIPT a in PIPT_Entries)
                if (a.ModelAssetID == assetID)
                    return true;

            return base.HasReference(assetID);
        }

        public override void Verify(ref List<string> result)
        {
            foreach (EntryPIPT a in PIPT_Entries)
            {
                if (a.ModelAssetID == 0)
                    result.Add("PIPT entry with ModelAssetID set to 0");
                Verify(a.ModelAssetID, ref result);
            }
        }

        [Category("Pipe Table")]
        public EntryPIPT[] PIPT_Entries
        {
            get
            {
                List<EntryPIPT> entries = new List<EntryPIPT>();
                
                for (int i = 4; i < Data.Length; i += EntryPIPT.SizeOfStruct(game))
                {
                    byte[] Flags = BitConverter.GetBytes(ReadInt(i + 8));

                    EntryPIPT a = new EntryPIPT()
                    {
                        ModelAssetID = ReadUInt(i),
                        MeshIndex = ReadInt(i + 4),
                        RelatedToVisibility = Flags[3],
                        Culling = Flags[2],
                        DestinationSourceBlend = Flags[1],
                        OtherFlags = Flags[0]
                    };

                    if (game == Game.Incredibles)
                    {
                        a.Unknown0C = ReadByte(i + 12);
                        a.Unknown0D = ReadByte(i + 13);
                        a.Unknown0E = ReadByte(i + 14);
                        a.Unknown0F = ReadByte(i + 15);
                    }

                    entries.Add(a);
                }
                
                return entries.ToArray();
            }
            set
            {
                List<byte> newData = new List<byte>();
                newData.AddRange(BitConverter.GetBytes(Switch(value.Length)));

                foreach (EntryPIPT i in value)
                {
                    newData.AddRange(BitConverter.GetBytes(Switch(i.ModelAssetID)));
                    newData.AddRange(BitConverter.GetBytes(Switch(i.MeshIndex)));
                    int Flags = BitConverter.ToInt32(new byte[] { i.OtherFlags, i.DestinationSourceBlend, i.Culling, i.RelatedToVisibility }, 0);
                    newData.AddRange(BitConverter.GetBytes(Switch(Flags)));

                    if (game == Game.Incredibles)
                    {
                        newData.Add(i.Unknown0C);
                        newData.Add(i.Unknown0D);
                        newData.Add(i.Unknown0E);
                        newData.Add(i.Unknown0F);
                    }
                }
                
                Data = newData.ToArray();
            }
        }

        public void Merge(AssetPIPT assetPIPT)
        {
            List<EntryPIPT> entriesPIPT = PIPT_Entries.ToList();
            List<uint> assetIDsAlreadyPresent = new List<uint>();

            foreach (EntryPIPT entryPIPT in entriesPIPT)
                assetIDsAlreadyPresent.Add(entryPIPT.ModelAssetID);

            foreach (EntryPIPT entryPIPT in assetPIPT.PIPT_Entries)
                if (!assetIDsAlreadyPresent.Contains(entryPIPT.ModelAssetID))
                    entriesPIPT.Add(entryPIPT);

            PIPT_Entries = entriesPIPT.ToArray();
        }
    }
}