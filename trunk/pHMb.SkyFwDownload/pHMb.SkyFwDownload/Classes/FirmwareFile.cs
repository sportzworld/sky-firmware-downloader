//Copyright (C) 2012 Matthew Thornhill (mrmt32@ph-mb.com)

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; either version 2
//of the License, or (at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using pHM.DVBLib.Common;
using System.Text.RegularExpressions;

namespace pHMb.TS_Demux
{
    public class FirmwareFile
    {
        public static Dictionary<ushort, string> Vendors = new Dictionary<ushort,string>() 
        {
            {0x0F, "Panasonic"},
            {0x9F, "Pace"},
            {0x4F, "Amstrad"},
            {0x6F, "Sony"},
            {0x4E, "Grundig"},
            {0x97, "Samsung"}
        };

        public static Dictionary<ushort, string> Models = new Dictionary<ushort, string>() 
        {
            {0x4F21, "Sky+ PVR2"},
            {0x4F22, "Sky+ PVR3"},
            {0x4F30, "DRX780"},
            {0x4F31, "DRX895"},
            {0x4F70, "DRX595"},
            {0x9F0C, "Unknown (0C)"},
            {0x9F21, "Sky+ PVR2"},
            {0x9F22, "Sky+ PVR3"},
            {0x9F30, "TDS850NB"},
            {0x9730, "HDSKY"}
        };

        public Dictionary<string, Extractors.IFirmwareExtractor> ExtractorList { get; set; }

        public string EPGVersion { get; private set; }

        public string FileName
        {
            get
            {
                return string.Format("{0}-FlashImage.bin", BaseName);
            }
        }

        public string BaseName
        {
            get
            {
                return string.Format("{0}_{1}_{2:X2}-{3:X2}-r{4}", VendorName, ModelName, VendorId, ModelId, VersionId);
            }
        }

        public ushort VendorId { get; set; }
        public ushort ModelId { get; private set; }
        public ushort VersionId { get; private set; }
        public uint FileSize { get; private set; }
        public uint BlockSize { get; private set; }

        public HashSet<uint> CompletedOffsets { get; private set; }

        public FileStream FirmwareStream { get; private set; }

        public string VendorName
        {
            get
            {
                if (Vendors.ContainsKey(VendorId))
                {
                    
                    return Vendors[VendorId];
                }
                else
                {
                    return string.Format("Unknown ({0:X2})", VendorId);
                }
            }
        }

        public string ModelName
        {
            get
            {
                if (Models.ContainsKey((ushort)((VendorId << 8) | ModelId)))
                {
                    return Models[(ushort)((VendorId << 8) | ModelId)];
                }
                else
                {
                    return string.Format("Unknown ({0:X2})", ModelId);
                }
            }
        }

        public Extractors.IFirmwareExtractor Extractor
        {
            get
            {
                string fwId = string.Format("{0:X2}{1:X2}", VendorId, ModelId);

                if (ExtractorList != null && ExtractorList.ContainsKey(fwId))
                {
                    return ExtractorList[fwId];
                }
                else
                {
                    return null;
                }
            }
        }

        public DateTime FwCreateDate { get; private set; }

        public string RootFSType
        {
            get
            {
                if (Extractor == null) return "?";
                else
                {
                    return Extractor.RootFsType;
                }
            }
        }

        public string SaveDirectory { get; private set; }

        public uint BytesCompleted { get; private set; }
        public bool isComplete { get; private set; }

        public double Progress
        {
            get
            {
                return (100 * (double)BytesCompleted) / FileSize;
            }
        }

        private void SetExtractorList()
        {

            Extractors.IFirmwareExtractor amstradCpioExtractor = new Extractors.AmstradRamfsKernelExtractor();
            ExtractorList = new Dictionary<string, Extractors.IFirmwareExtractor>()
            {
                {"4F70", amstradCpioExtractor},
                {"4F31", amstradCpioExtractor},
                {"9730", new Extractors.SamsungExtractor()},
                {"4F30", new Extractors.AmstradSquashfsExtractor()},
                {"9F30", new Extractors.PaceExtractor()}
            };
        }

        public FirmwareFile(FwPacket firmwarePacket, string saveDirectory)
        {
            VendorId = firmwarePacket.VendorId;
            ModelId = firmwarePacket.ModelId;
            FileSize = firmwarePacket.FileLength;
            BlockSize = (uint)firmwarePacket.Data.Length;
            VersionId = firmwarePacket.FirmwareVersion;
            BytesCompleted = 0;
            isComplete = false;
            SaveDirectory = saveDirectory;

            CompletedOffsets = new HashSet<uint>();

            FirmwareStream = File.Create(Path.Combine(SaveDirectory, string.Format("{0}.tmp", BaseName)));

            SetExtractorList();

            AddPacket(firmwarePacket);
        }

        public FirmwareFile(string folderPath)
        {
            SaveDirectory = Path.GetDirectoryName(folderPath);
            DirectoryInfo fwDir = new DirectoryInfo(folderPath);
            string baseName = fwDir.Name;

            string[] split1 = baseName.Split('_');
            string[] split2 = split1[2].Split('-');

            VendorId = Convert.ToUInt16(split2[0], 16);
            ModelId = Convert.ToUInt16(split2[1], 16);
            VersionId = ushort.Parse(split2[2].Substring(1));

            if (File.Exists(Path.Combine(folderPath, "date_stamp.txt")))
            {
                try
                {
                    FwCreateDate = new DateTime(long.Parse(File.ReadAllText(Path.Combine(folderPath, "date_stamp.txt"))));
                }
                catch (Exception ex)
                {
                    Logger.AddLogItem(string.Format("Error getting creation date for {0}.", baseName), "FirmwareFile", LogLevels.Warning, ex);
                }
            }

            SetExtractorList();

            FileInfo fwInfo = new FileInfo(Path.Combine(folderPath, FileName));
            FileSize = (uint)fwInfo.Length;
            BytesCompleted = (uint)fwInfo.Length;

            GetVersion();

            isComplete = true;
        }

        public bool AddPacket(FwPacket firmwarePacket)
        {
            if (VendorId == firmwarePacket.VendorId && ModelId == firmwarePacket.ModelId
                && firmwarePacket.hasPassedCRC && firmwarePacket.SectionType == 1 && !isComplete)
            {
                if (firmwarePacket.Data.Length != BlockSize)
                {
                    //Console.WriteLine("Blocksize differs! At offset: 0x{0:X2}", firmwarePacket.FileOffset);
                }

                if (!CompletedOffsets.Contains(firmwarePacket.FileOffset))
                {
                    FirmwareStream.Seek(firmwarePacket.FileOffset, SeekOrigin.Begin);
                    FirmwareStream.Write(firmwarePacket.Data, 0, firmwarePacket.Data.Length);
                    BytesCompleted += (uint)firmwarePacket.Data.Length;

                    CompletedOffsets.Add(firmwarePacket.FileOffset);

                    if (BytesCompleted >= FileSize)
                    {
                        FirmwareStream.Close();
                        DirectoryInfo fwDir = Directory.CreateDirectory(Path.Combine(SaveDirectory, BaseName));

                        File.Move(Path.Combine(SaveDirectory, string.Format("{0}.tmp", BaseName)), Path.Combine(fwDir.FullName, FileName));
                        isComplete = true;

                        FwCreateDate = DateTime.Now;
                        File.WriteAllText(Path.Combine(fwDir.FullName, "date_stamp.txt"), FwCreateDate.Ticks.ToString());
                        

                        Extractors.IFirmwareExtractor currentExtractor;
                        if ((currentExtractor = Extractor) != null)
                        {
                            try
                            {
                                currentExtractor.ExtractParts(Path.Combine(fwDir.FullName, FileName));
                                GetVersion();
                            }
                            catch (Exception ex)
                            {
                                Logger.AddLogItem("Error extracting firmware!", "FirmwareFile", LogLevels.Warning, ex);
                            }
                        }
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private void GetVersion()
        {
            if (File.Exists(System.IO.Path.Combine(SaveDirectory, BaseName, @"config\version.cfg")))
            {
                string versionIniFile = File.ReadAllText(System.IO.Path.Combine(SaveDirectory, BaseName, @"config\version.cfg"));

                Match match = Regex.Match(versionIniFile, "NDS_SW_VERSION=\"([^\r\n]*)\"");

                if (match.Success)
                {
                    EPGVersion = match.Groups[1].Value;
                }
            }
        }
    }
}
