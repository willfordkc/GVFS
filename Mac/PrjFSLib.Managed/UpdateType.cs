﻿using System;

namespace PrjFSLib.Managed
{
    [Flags]
    public enum UpdateType
    {
        Invalid         = 0x00000000,

        AllowDirtyData  = 0x00000002,
        AllowReadOnly   = 0x00000020,
    }
}
