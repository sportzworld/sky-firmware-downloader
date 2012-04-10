using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.TS_Demux
{
    public struct FwPacket
    {
        public ushort Length;
        public uint SectionType;
        public uint Unknown;
        public ushort VendorId;
        public ushort ModelId;
        public ushort FirmwareVersion;
        public uint FileOffset;
        public uint FileLength;
        public uint Unknown2;
        public byte[] Data;
        public uint Crc32;
        public bool hasPassedCRC;
    }

}
