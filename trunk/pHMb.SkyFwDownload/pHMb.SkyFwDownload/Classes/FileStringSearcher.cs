using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace pHMb.TS_Demux
{
    public static class FileStringSearcher
    {
        public static int FindInFile(FileStream file, string value)
        {   // returns complement of number of characters in file if not found
            // else returns index where value found
            int index = 0;
            file.Seek(0, SeekOrigin.Begin);
            System.IO.StreamReader reader = new System.IO.StreamReader(file, Encoding.ASCII);

            if (String.IsNullOrEmpty(value))
                return 0;
            StringSearch valueSearch = new StringSearch(value);
            int readChar;
            while ((readChar = reader.Read()) >= 0)
            {
                ++index;
                if (valueSearch.Found(readChar))
                    return index - value.Length;
            }
            return ~index;
        }

        public class StringSearch
        {   // Call Found one character at a time until string found
            private readonly string value;
            private readonly List<int> indexList = new List<int>();
            public StringSearch(string value)
            {
                this.value = value;
            }
            public bool Found(int nextChar)
            {
                for (int index = 0; index < indexList.Count; )
                {
                    int valueIndex = indexList[index];
                    if (value[valueIndex] == nextChar)
                    {
                        ++valueIndex;
                        if (valueIndex == value.Length)
                        {
                            indexList[index] = indexList[indexList.Count - 1];
                            indexList.RemoveAt(indexList.Count - 1);
                            return true;
                        }
                        else
                        {
                            indexList[index] = valueIndex;
                            ++index;
                        }
                    }
                    else
                    {   // next char does not match
                        indexList[index] = indexList[indexList.Count - 1];
                        indexList.RemoveAt(indexList.Count - 1);
                    }
                }
                if (value[0] == nextChar)
                {
                    if (value.Length == 1)
                        return true;
                    indexList.Add(1);
                }
                return false;
            }
            public void Reset()
            {
                indexList.Clear();
            }
        }
    }
}
