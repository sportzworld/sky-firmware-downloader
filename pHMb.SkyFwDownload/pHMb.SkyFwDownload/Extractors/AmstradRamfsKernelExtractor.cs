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
using System.IO.Compression;
using System.Diagnostics;

namespace pHMb.TS_Demux.Extractors
{
    /// <summary>
    /// Extractor for format currently used by DRX595 & DRX895
    /// </summary>
    public class AmstradRamfsKernelExtractor : IFirmwareExtractor
    {
        public string RootFsType
        {
            get
            {
                return "LZMA CPIO";
            }
        }

        public string KernelType
        {
            get
            {
                return "gz";
            }
        }

        public void ExtractParts(string imageFilename)
        {
            string extractDir = Path.GetDirectoryName(imageFilename);

            using (FileStream imageStream = File.Open(imageFilename, FileMode.Open))
            {
                using (FileStream cfeStream = File.Create(Path.Combine(extractDir, "cfe.bin")))
                {
                    Misc.CopyStream(imageStream, cfeStream, 0x7630, 0x189AC);
                }

                using (FileStream kernelStream = File.Create(Path.Combine(extractDir, "vmlinux.gz")))
                {
                    Misc.CopyStream(imageStream, kernelStream, 0x1FFDC, 0);

                    kernelStream.Seek(0, SeekOrigin.Begin);
                    using (GZipStream compKernelStream = new GZipStream(kernelStream, CompressionMode.Decompress))
                    {
                        using (FileStream uncompKernelStream = File.Create(Path.Combine(extractDir, "vmlinux")))
                        {
                            compKernelStream.CopyTo(uncompKernelStream);

                            // Now we want to try and find the lzma'd cpio filesystem

                            int cpioIndex = FileStringSearcher.FindInFile(uncompKernelStream, Encoding.ASCII.GetString(new byte[] 
                            { 
                                0x5D, 0x00, 0x00, 0x00, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF 
                            }));

                            if (cpioIndex != ~uncompKernelStream.Length)
                            {
                                using(FileStream rootFsLzmaStream = File.Create(Path.Combine(extractDir, "filesystem.lzma")))
                                {
                                    Misc.CopyStream(uncompKernelStream, rootFsLzmaStream, cpioIndex, 0);
                                }

                                string appDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                                Process unlzmaProcess = new Process();
                                unlzmaProcess.StartInfo.FileName = Path.Combine(appDir, "lzma.exe");
                                unlzmaProcess.StartInfo.Arguments = string.Format("d \"{0}\" \"{1}\"", Path.Combine(extractDir, "filesystem.lzma"),
                                    Path.Combine(extractDir, "filesystem.bin"));

                                unlzmaProcess.Start();
                                unlzmaProcess.WaitForExit();

                                if (File.Exists(Path.Combine(extractDir, "filesystem.bin")))
                                {
                                    Directory.CreateDirectory(Path.Combine(extractDir, "rootfs"));

                                    Process cpioProcess = new Process();
                                    cpioProcess.StartInfo.FileName = Path.Combine(appDir, "extract_fs.cmd");
                                    cpioProcess.StartInfo.Arguments = string.Format("\"{0}\" \"{1}\" \"{2}\"", Path.Combine(extractDir, "rootfs"),
                                        Path.Combine(appDir, "cpio.exe"),
                                        Path.Combine(extractDir, "filesystem.bin"));

                                    cpioProcess.Start();
                                    cpioProcess.WaitForExit();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
