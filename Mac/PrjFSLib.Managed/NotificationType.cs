﻿using System;

namespace PrjFSLib.Managed
{
    [Flags]
    public enum NotificationType
    {
        Invalid             = 0x00000000,

        None                = 0x00000001,
        NewFileCreated      = 0x00000004,
        PreDelete           = 0x00000010,
        FileRenamed         = 0x00000080,
        PreConvertToFull    = 0x00001000,

        PreModify           = 0x10000001,
        FileModified        = 0x10000002,
        FileDeleted         = 0x10000004,
    }
}
