using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pHM.DVBLib.Common;
using System.IO;
using System.Runtime.InteropServices;

namespace pHMb.TS_Demux
{
    public class ConfigKeyValuePair : BinaryClass
    {
        public ConfigKeyValuePair(byte[] data, int startOffset, bool isLittleEndian) : base(data, startOffset, isLittleEndian) { }

        public byte TypeCode
        {
            get
            {
                return ReadByte(0);
            }
        }

        public Type ConfigType
        {
            get
            {
                byte rawType = ReadByte(0);

                switch (rawType)
                {
                    case 13:                    // Int32 (signed)
                        return typeof(int);

                    case 11:                    // Displayed as hex
                        return typeof(uint);

                    case 9:
                        return typeof(uint);

                    case 4:                     // Bool
                        return typeof(bool);

                    case 2:                     // String
                        return typeof(string);

                    default:
                        return typeof(object);
                }
            }
        }

        public byte NameLength
        {
            get
            {
                return ReadByte(1);
            }
        }

        public int NameOffset
        {
            get
            {
                return ConfigType == typeof(string) ? 4 : 2;
            }
        }

        public string Name
        {
            get
            {
                return Encoding.ASCII.GetString(this.ReadBytes(NameOffset, NameLength));
            }
        }

        private ushort StringLength
        {
            get
            {
                return ReadUInt16(2);
            }
        }

        public object Value
        {
            get 
            {
                if (ConfigType == typeof(string))
                {
                    return Encoding.ASCII.GetString(this.ReadBytes(NameOffset + NameLength, StringLength));
                }
                else if (ConfigType == typeof(object))
                {
                    throw new InvalidDataException("Unknown type code encountered");
                }
                else if (ConfigType == typeof(bool))
                {
                    return BitConverter.ToBoolean(this.ReadBytes(NameOffset + NameLength, 1), 0);
                }
                else if (ConfigType == typeof(int))
                {
                    return (int)this.ReadUInt32(NameOffset + NameLength);
                }
                else if (ConfigType == typeof(uint))
                {
                    return this.ReadUInt32(NameOffset + NameLength);
                }
                else if (ConfigType == typeof(ushort))
                {
                    return this.ReadUInt16(NameOffset + NameLength);
                }
                else
                {
                    return this.ReadBytes(NameOffset + NameLength, Marshal.SizeOf(ConfigType));
                }
            }
        }

        public T FromByteArray<T>(byte[] rawValue)
        {
            GCHandle handle = GCHandle.Alloc(rawValue, GCHandleType.Pinned);
            T structure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return structure;
        }

        public uint Size
        {
            get
            {
                int valueSize = 0;

                if (ConfigType == typeof(string))
                {
                    valueSize = StringLength;
                }
                else if (ConfigType == typeof(bool))
                {
                    valueSize = 1;
                }
                else if (ConfigType == typeof(int))
                {
                    valueSize = 4;
                }
                else if (ConfigType == typeof(uint))
                {
                    valueSize = 4;
                }
                else if (ConfigType == typeof(ushort))
                {
                    valueSize = 2;
                }

                return (uint)((ConfigType == typeof(string) ? 2 : 0) + valueSize + NameLength + 2);
            }
        }
    }
}
