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
