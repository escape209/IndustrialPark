﻿using HipHopFile;

namespace IndustrialPark
{
    public class AssetGeneric : Asset
    {
        public AssetGeneric(Section_AHDR AHDR) : base(AHDR)
        {
        }

        public override void Setup(bool defaultMode = true) { }
    }
}