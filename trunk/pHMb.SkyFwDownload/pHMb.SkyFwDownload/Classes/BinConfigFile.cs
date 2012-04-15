using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pHM.DVBLib.Common;

namespace pHMb.TS_Demux
{
    public class BinConfigFile : BinaryClass
    {
        public BinConfigFile(byte[] data, bool isLittleEndian) : base(data, 0, isLittleEndian) { }

        public uint HeaderLength
        {
            get
            {
                return this.ReadUInt32(0);
            }
        }

        private List<ConfigFileDescriptor> _fileDescriptors;
        public List<ConfigFileDescriptor> FileDescriptors
        {
            get
            {
                if (_fileDescriptors == null)
                {
                    _fileDescriptors = new List<ConfigFileDescriptor>();

                    ConfigFileDescriptor currentFileDescriptor;
                    for (uint i = 0; i < HeaderLength; i += currentFileDescriptor.DescriptorSize)
                    {
                        currentFileDescriptor = new ConfigFileDescriptor(Data, (int)(i + 4), this.IsLittleEndian);
                        _fileDescriptors.Add(currentFileDescriptor);
                    }
                }
                return _fileDescriptors;
            }
        }
    }
}
