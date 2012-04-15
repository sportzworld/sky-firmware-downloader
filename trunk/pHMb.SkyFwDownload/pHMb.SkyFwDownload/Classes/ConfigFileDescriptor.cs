using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pHM.DVBLib.Common;

namespace pHMb.TS_Demux
{
    public class ConfigFileDescriptor : BinaryClass
    {
        public ConfigFileDescriptor(byte[] data, int startOffset, bool isLittleEndian) : base(data, startOffset, isLittleEndian) { }

        public byte NameLength
        {
            get
            {
                return this.ReadByte(0);
            }
        }

        public string Name
        {
            get
            {
                return Encoding.ASCII.GetString(this.ReadBytes(1, NameLength));
            }
        }

        public uint Position
        {
            get
            {
                return this.ReadUInt32(NameLength + 1);
            }
        }

        public uint DescriptorSize
        {
            get
            {
                return (uint)(NameLength + 5);
            }
        }

        private Dictionary<string, Dictionary<string, object>> _sections;
        public Dictionary<string, Dictionary<string, object>> Sections
        {
            get
            {
                if (_sections == null)
                {
                    _sections = new Dictionary<string, Dictionary<string, object>>();

                    int currentPosition = (int)Position;

                    ConfigSectionDescriptor currentDescriptor;
                    while (this.ReadUInt32(currentPosition - this.StartOffset) != Position)
                    {
                        currentDescriptor = new ConfigSectionDescriptor(Data, currentPosition, IsLittleEndian);
                        _sections.Add(currentDescriptor.Name, currentDescriptor.KeyValuePairs);

                        currentPosition += (int)currentDescriptor.Size;
                    }
                }

                return _sections;
            }
        }
    }
}
