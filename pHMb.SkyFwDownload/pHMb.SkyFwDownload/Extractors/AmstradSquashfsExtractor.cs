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
using System.IO.Compression;
using pHM.DVBLib.Common;
using System.Diagnostics;

namespace pHMb.TS_Demux.Extractors
{
    class AmstradSquashfsExtractor : IFirmwareExtractor
    {
        public string RootFsType
        {
            get { return "SquashFS"; }
        }

        public string KernelType
        {
            get { return "gz"; }
        }

        public void ExtractParts(string imageFilename)
        {
            string extractDir = Path.GetDirectoryName(imageFilename);

            using (FileStream imageStream = File.Open(imageFilename, FileMode.Open))
            {
                using (FileStream kernelStream = File.Create(Path.Combine(extractDir, "vmlinux.gz")))
                {
                    Misc.CopyStream(imageStream, kernelStream, 0x4343C, 0);

                    kernelStream.Seek(0, SeekOrigin.Begin);
                    using (GZipStream compKernelStream = new GZipStream(kernelStream, CompressionMode.Decompress))
                    {
                        using (FileStream uncompKernelStream = File.Create(Path.Combine(extractDir, "vmlinux")))
                        {
                            compKernelStream.CopyTo(uncompKernelStream);
                        }
                    }
                }
                
                int squashIndex = FileStringSearcher.FindInFile(imageStream, "hsqs");

                if (squashIndex != ~imageStream.Length)
                {
                    using (FileStream rootfsStream = File.Create(Path.Combine(extractDir, "filesystem.squashfs.bin")))
                    {
                        Misc.CopyStream(imageStream, rootfsStream, squashIndex, imageStream.Length - squashIndex);
                    }

                    string appDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    if (Directory.Exists(Path.Combine(extractDir, "rootfs"))) Directory.Delete(Path.Combine(extractDir, "rootfs"));

                    Process unsquashProcess = new Process();
                    unsquashProcess.StartInfo.FileName = Path.Combine(appDir, "unsquashfs.exe");
                    unsquashProcess.StartInfo.Arguments = string.Format("-d \"{0}\" \"{1}\"", Path.Combine(extractDir, "rootfs"),
                        Path.Combine(extractDir, "filesystem.squashfs.bin"));

                    unsquashProcess.Start();
                    unsquashProcess.WaitForExit();
                }
            }
        }
    }
}
