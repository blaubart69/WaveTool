using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace WaveWork
{
    /*
     * 2014-07-01 Spindler
     *  deprecated because I've found out that the WAV header is more "dynamic" as I though in the beginning. :-)
     * 
     * 
    public struct WaveHeader
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string RiffHeaderString;
        public UInt32 FilelengthMinus8;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string WaveHeaderString;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string fmtHeaderString;
        public UInt32 fmtLength;
        public UInt16 fmtTag;       // 1 = PCM
        public UInt16 Channels;
        public UInt32 SampleRate;
        public UInt32 BytesPerSecond;
        public UInt16 BlockAlign;
        public UInt16 BitsPerSample;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string DataHeaderString;

        public UInt32 DataLength;
    }
    */
    public struct WaveHeader2
    {
        public string RiffHeaderString;
        public UInt32 FilelengthMinus8;
        public string WaveHeaderString;

        public string fmtHeaderString;
        public UInt32 fmtLength;
        public UInt16 fmtTag;       // 1 = PCM
        public UInt16 Channels;
        public UInt32 SampleRate;
        public UInt32 BytesPerSecond;
        public UInt16 BlockAlign;
        public UInt16 BitsPerSample;
        public UInt16 cbSize;

        public string DataHeaderString;
        public UInt32 DataLength;
    }

    public static class WaveTools
    {
        public static int DotsWidth = 25;

        public static WaveHeader2 ReadHeader(BinaryReader br)
        {
            WaveHeader2 wh = new WaveHeader2();
            wh.RiffHeaderString = new String( br.ReadChars(4) );
            wh.FilelengthMinus8 = br.ReadUInt32();
            wh.WaveHeaderString = new String( br.ReadChars(4) );
            wh.fmtHeaderString = new String( br.ReadChars(4));
            wh.fmtLength        = br.ReadUInt32();
            
            //
            // 16 bytes to come...
            //
            wh.fmtTag           = br.ReadUInt16();
            wh.Channels         = br.ReadUInt16();
            wh.SampleRate       = br.ReadUInt32();
            wh.BytesPerSecond   = br.ReadUInt32();
            wh.BlockAlign       = br.ReadUInt16();
            wh.BitsPerSample    = br.ReadUInt16();
            if (wh.fmtLength == 16)
            {
                // everything ok.
            }
            else if (wh.fmtLength == 18)
            {
                wh.cbSize = br.ReadUInt16();
                Console.WriteLine("extension size: [{0}]", wh.cbSize);
            }
            if (wh.cbSize > 0)
            {
                Console.WriteLine("skipping extension bytes");
                br.ReadBytes(wh.cbSize);
            }

            /*
             * 1. read 4 bytes of "chunk name"
             * 2. read 4 bytes of "chunk length"
             * 3. if the chunk is not the "data" chunk => skip the bytes and read the next chunk
             */

            string ChunkName;
            UInt32 ChunkLen;
            bool dataChunkFound = false;
            do 
            {
                ChunkName = new String( br.ReadChars(4) );
                Console.WriteLine("chunk header read [{0}]", ChunkName);
                ChunkLen = br.ReadUInt32();
                Console.WriteLine("chunk length [{0}]", ChunkLen);
                if (ChunkName.ToLower().Equals("data"))
                {
                    Console.WriteLine("found [data] chunk. stopping to read further.");
                    dataChunkFound = true;
                }
                else
                {
                    Console.WriteLine("skipping {0} bytes", ChunkLen);
                    br.ReadBytes( (int)ChunkLen );
                }
            } 
            while ( !dataChunkFound );

            wh.DataHeaderString = ChunkName;
            wh.DataLength       = ChunkLen;

            return wh;
        }
        public static void PrintHeader(WaveHeader2 wh)
        {
            Console.WriteLine("----- WAV HEADER (start) -----");
            WaveTools.PrintWithDots("RIFF header",      wh.RiffHeaderString);
            WaveTools.PrintWithDots("Filelength - 8",   wh.FilelengthMinus8.ToString());
            WaveTools.PrintWithDots("Wave header",      wh.WaveHeaderString);
            WaveTools.PrintWithDots("Format header",    wh.fmtHeaderString);
            WaveTools.PrintWithDots("Format length",    wh.fmtLength.ToString());

            WaveTools.PrintWithDots("Format tag (1=PCM)",   wh.fmtTag.ToString());
            WaveTools.PrintWithDots("Channels",             wh.Channels.ToString());
            WaveTools.PrintWithDots("Samplerate",           wh.SampleRate.ToString());
            WaveTools.PrintWithDots("Bytes/s",              wh.BytesPerSecond.ToString());
            WaveTools.PrintWithDots("Block align",          wh.BlockAlign.ToString());
            WaveTools.PrintWithDots("bits/sample",          wh.BitsPerSample.ToString());
            if (wh.fmtLength == 18)
            {
                WaveTools.PrintWithDots("cbSize",           wh.cbSize.ToString());
            }

            WaveTools.PrintWithDots("Data header",          wh.DataHeaderString);
            WaveTools.PrintWithDots("Data length",          wh.DataLength.ToString());
            Console.WriteLine("----- WAV HEADER (end) ------");
        }
        /*
        public static WaveHeader BytesToStruct(byte[] packet)
        {
            GCHandle pinnedPacket = GCHandle.Alloc(packet, GCHandleType.Pinned);

            WaveHeader wh = (WaveHeader)Marshal.PtrToStructure(
                                        pinnedPacket.AddrOfPinnedObject(),
                                        typeof(WaveHeader));
            pinnedPacket.Free();

            return wh;
        }
        */
        public static Int16 GetSampleValue(byte[] Bytes)
        {
            Int16 Sample16Bit = Bytes[1];
            Sample16Bit = (Int16)(Sample16Bit << 8);
            Sample16Bit = (Int16)(Sample16Bit | (Int16)Bytes[0]);
            return Sample16Bit;
        }
        public static Int16 GetMonoValue(BinaryReader br)
        {
            byte[] value = br.ReadBytes(2);
            if (value.Length < 2)
            {
                throw new Exception("EOF");
            }
            return GetSampleValue(value);
        }
        public static UInt16 CalcRmsValue(BinaryReader br, WaveHeader2 wh)
        {
            UInt64 HelpValue = 0;

            if (wh.Channels == 1)
            {
                Int16 MonoVal = WaveTools.GetMonoValue(br);
                HelpValue = (UInt64)Math.Abs((Int32)MonoVal);
            }
            else if (wh.Channels == 2)
            {
                //Int16[] StereoVal = WaveTools.GetStereoValues(br);

                //HelpValue = Math.Abs((Int32)(StereoVal[0])) + Math.Abs((Int32)(StereoVal[1]));
                //HelpValue = HelpValue / 2;

                /*
                UInt64 left = (UInt64)Math.Abs((Int32)(StereoVal[0]));
                UInt64 right = (UInt64)Math.Abs((Int32)(StereoVal[1]));
                */
                UInt64 left  = (UInt64)Math.Abs((Int32)(WaveTools.GetMonoValue(br)));
                UInt64 right = (UInt64)Math.Abs((Int32)(WaveTools.GetMonoValue(br)));


                HelpValue = left * left + right * right;
                HelpValue = HelpValue / 2;
                HelpValue = (UInt16)Math.Sqrt( HelpValue );


            }
            return (UInt16)HelpValue;
        }
        public static Int16[] GetStereoValues(BinaryReader br)
        {
            Int16[] StereoVal = new Int16[2];

            StereoVal[0] = GetMonoValue(br);
            StereoVal[1] = GetMonoValue(br);

            return StereoVal;
        }
        public static float FramesAsSeconds(UInt64 frame, uint Samplingrate)
        {
            return (float)frame / (float)Samplingrate;
        }
        public static string FramesAsMinutesSeconds(UInt64 frame, uint Samplingrate)
        {
            UInt64 sek = frame / Samplingrate;
            UInt64 s = sek % 60;
            UInt64 m = sek / 60;

            return string.Format("{0}m {1}s", m, s);
        }
        public static uint GetFrameCount(WaveHeader2 wh)
        {
            uint NumberAvgVals = wh.FilelengthMinus8 - 36;
            if (wh.Channels == 2)
            {
                NumberAvgVals = NumberAvgVals / 2;
            }
            if (wh.fmtLength == 16)
            {
                NumberAvgVals = NumberAvgVals / 2;
            }
            return NumberAvgVals;
        }
        public static int SizeOf()
        {
            Type tt = typeof(WaveHeader2);
            int size;
            if (tt.IsValueType)
            {
                Console.WriteLine("{0} is a value type", tt.Name);
                size = Marshal.SizeOf(tt);
            }
            else
            {
                Console.WriteLine("{0} is a reference type", tt.Name);
                size = IntPtr.Size;
            }
            Console.WriteLine("Size = {0}", size);
            return size;
        }
        public static string PrintWithDots(string s)
        {
            return (s + " ").PadRight(DotsWidth,'.') + " ";
        }
        public static void PrintWithDots(string s, string value)
        {
            Console.WriteLine("{0}{1}",PrintWithDots(s),value);
        }
    }
}
