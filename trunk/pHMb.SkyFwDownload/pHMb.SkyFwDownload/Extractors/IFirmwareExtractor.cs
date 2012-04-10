using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.TS_Demux.Extractors
{
    public interface IFirmwareExtractor
    {
        string RootFsType { get; }
        string KernelType { get; }

        void ExtractParts(string imageFilename);
    }
}
