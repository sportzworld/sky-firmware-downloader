using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace DirectShowLib.Sample
{
    class TsGrabber : ISampleGrabberCB
    {
        public delegate void ProcessBufferDelegate(byte[] buffer);

        byte[] buffer;

        public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        {
            if (buffer == null || buffer.Length < BufferLen)
            {
                buffer = new byte[BufferLen];
            }

            Marshal.Copy(pBuffer, buffer, 0, BufferLen);

            Callback(buffer);
            return 0;
        }

        public int SampleCB(double SampleTime, IMediaSample pSample)
        {
            throw new NotImplementedException();
        }

        public ProcessBufferDelegate Callback;
    }
}
