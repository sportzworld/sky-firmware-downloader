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
