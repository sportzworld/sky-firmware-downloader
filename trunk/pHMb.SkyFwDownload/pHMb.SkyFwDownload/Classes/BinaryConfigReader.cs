using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace pHMb.TS_Demux
{
    public class BinaryConfigReader
    {
        public string FileName { get; private set; }
        public BinConfigFile BinaryConfigFile { get; private set; }

        public BinaryConfigReader(string filename, bool isLittleEndian)
        {
            FileName = filename;

            byte[] configFile = File.ReadAllBytes(filename);
            BinaryConfigFile = new BinConfigFile(configFile, isLittleEndian);
        }

        public void SaveAsINI(string outputFolder)
        {
            foreach (ConfigFileDescriptor fileDescriptor in BinaryConfigFile.FileDescriptors)
            {
                using (FileStream iniFile = File.Create(Path.Combine(outputFolder, fileDescriptor.Name)))
                {
                    using (StreamWriter iniWriter = new StreamWriter(iniFile, Encoding.ASCII))
                    {
                        foreach (KeyValuePair<string, Dictionary<string, object>> section in fileDescriptor.Sections)
                        {
                            iniWriter.WriteLine("[{0}]", section.Key);

                            foreach (KeyValuePair<string, object> kvp in section.Value)
                            {
                                if (kvp.Value.GetType() == typeof(string))
                                {
                                    iniWriter.WriteLine("{0}=\"{1}\"", kvp.Key, kvp.Value);
                                }
                                else
                                {
                                    iniWriter.WriteLine("{0}={1}", kvp.Key, kvp.Value);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
