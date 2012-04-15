using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pHM.DVBLib.Common;

namespace pHMb.TS_Demux
{
    public class ConfigSectionDescriptor : BinaryClass
    {
        public ConfigSectionDescriptor(byte[] data, int startOffset, bool isLittleEndian) : base(data, startOffset, isLittleEndian) { }

        //ubyte  Unknown1;
        //ushort  Unknown2;
        public ushort SectionLength
        {
            get
            {
                return ReadUInt16(3);
            }
        }
        
        public byte NameLength
        {
            get
            {
                return ReadByte(5);
            }
        }

        public string Name
        {
            get
            {
                return Encoding.ASCII.GetString(this.ReadBytes(6, NameLength));
            }
        }

        public uint DescriptorSize
        {
            get
            {
                return (uint)(NameLength + 6);
            }
        }

        public uint Size
        {
            get
            {
                return (uint)(NameLength + 6 + _keyValuePairsSize);
            }
        }

        private uint _keyValuePairsSize = 0;
        private Dictionary<string, object> _keyValuePairs;
        public Dictionary<string, object> KeyValuePairs
        {
            get
            {
                if (_keyValuePairs == null)
                {
                    _keyValuePairs = new Dictionary<string, object>();

                    // Seek to start of config file
                    this.DataReader.BaseStream.Seek(this.StartOffset + DescriptorSize, System.IO.SeekOrigin.Begin);

                    ConfigKeyValuePair currentKVP;
                    for (uint i = 0; i < SectionLength; i += currentKVP.Size)
                    {
                        currentKVP = new ConfigKeyValuePair(Data, (int)(i + DescriptorSize + StartOffset), this.IsLittleEndian);
                        _keyValuePairs.Add(currentKVP.Name, currentKVP.Value);
                        _keyValuePairsSize += currentKVP.Size;
                    }
                }

                return _keyValuePairs;
            }
        }
    }
}
