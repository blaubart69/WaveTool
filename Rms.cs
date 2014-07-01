﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WaveWork
{
    class Rms
    {
        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern long StrFormatByteSize(
                long fileSize
                , [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer
                , int bufferSize);

        public static string GetPrettyByteSize(long size)
        {
            StringBuilder sb = new StringBuilder(20);
            StrFormatByteSize(size, sb, 20);
            return sb.ToString();
        }

        private static void PrintRmsResult(double BiggestRms, ulong RmsStartPos, uint SampleRate, ulong BytesProcessed, DateTime Start)
        {
            Console.WriteLine("{0}{1}",
                WaveTools.PrintWithDots("Biggest Rms"),
                (double)BiggestRms / Int16.MaxValue);

            Console.WriteLine("{0}{1} ({2})",
                WaveTools.PrintWithDots("Start at"),
                WaveTools.FramesAsSeconds(RmsStartPos, SampleRate).ToString("0.000"),
                WaveTools.FramesAsMinutesSeconds(RmsStartPos, SampleRate)
                );

            Console.WriteLine("{0}{1}",
                 WaveTools.PrintWithDots("OverallBytes processed"),
                 BytesProcessed);

            double millis = new TimeSpan( DateTime.Now.Ticks - Start.Ticks ).TotalMilliseconds;

            double BytesPerMilli = BytesProcessed / millis;
            long BytesPerSec = (long)(BytesPerMilli * 1000);

            Console.WriteLine("{0}{1}",
                WaveTools.PrintWithDots("MB/s"),
                GetPrettyByteSize( BytesPerSec ) );

        }

        private static void PrintProgressLine(ulong BytesProcessed, ulong OverallBytes, ulong FrameCount, uint SampleRate)
        {
            DateTime End = DateTime.Now;

            Console.Write("Position: ");
            Console.Write(WaveTools.FramesAsMinutesSeconds(FrameCount, SampleRate));
            Console.Write(" (");
            Console.Write( ((float)BytesProcessed / OverallBytes * 100).ToString("0") );
            Console.Write("%)     \r");
        }
        private static UInt64 GetAverageValueSqr(ushort Channels, byte[] Data, ref int pos, ref UInt64 BytesProcessed)
        {
            UInt64 SqrRms;

            if (Channels == 1)
            {
                UInt64 AvgVal;
                Int16 SampleValue = BitConverter.ToInt16(Data, pos);
                AvgVal = (UInt64)Math.Abs(SampleValue);
                pos += 2;
                SqrRms = AvgVal * AvgVal;
                BytesProcessed += 2;
            }
            else //if (Channels == 2)
            {
                UInt64 AvgVal;
                Int16 LeftChannel = BitConverter.ToInt16(Data, pos);
                pos += 2;
                Int16 RightChannel = BitConverter.ToInt16(Data, pos);
                pos += 2;

                UInt64 AbsLeft = (UInt64)Math.Abs(LeftChannel);
                UInt64 AbsRight = (UInt64)Math.Abs(RightChannel);

                AvgVal = AbsLeft * AbsLeft + AbsRight * AbsRight;
                SqrRms = AvgVal / 2;
                //AvgVal = (UInt64)Math.Sqrt(AvgVal);
                BytesProcessed += 4;
            }

            return SqrRms;
        }

        public static void CalcRms3(BinaryReader br, WaveHeader2 wh, uint AvgDurationFrames)
        {
            const   int     BufferSize = 16 * 1024 * 1000;
            byte[] Data;

            SpiBuffer FrameBufferSqr = new SpiBuffer(AvgDurationFrames);
            UInt64 FrameCount = 0;
            double BiggestRms = 0;
            UInt64 BiggestRmsStart = 0;
            UInt32 ProgressCounter = 0;
            UInt64 BytesProcessed = 0;

            DateTime StartTime = DateTime.Now;

            while ( (Data = br.ReadBytes(BufferSize)).Length != 0  )
            {
                int i=0;
                while ( i < Data.Length )
                {
                    UInt64 SqrRms = GetAverageValueSqr(wh.Channels, Data, ref i, ref BytesProcessed);

                    FrameBufferSqr.Add( SqrRms );
                    FrameCount++;

                    if (FrameBufferSqr.isFull)
                    {
                        // Den Durchschnittswert des SqrBuffers errechnen
                        double rms = Math.Sqrt((double)FrameBufferSqr.Sum / (double)AvgDurationFrames);

                        if (rms > BiggestRms)
                        {
                            BiggestRms = rms;
                            BiggestRmsStart = FrameCount - AvgDurationFrames;
                        }
                    }

                    ProgressCounter++;
                    if (ProgressCounter >= wh.SampleRate * 60) // every minute within the file
                    {
                        ProgressCounter = 0;
                        PrintProgressLine( BytesProcessed, wh.DataLength, FrameCount, wh.SampleRate);
                    }
                }
            }

            if (BytesProcessed == wh.DataLength)
            {
                PrintRmsResult(BiggestRms, BiggestRmsStart, wh.SampleRate, BytesProcessed, StartTime);
                Console.WriteLine("SUCCESS! (Bytes processed matches data length. WAV Header was read successfully.)");
            }
            else
            {
                Console.WriteLine("ERROR! The WAV File was no read successfully. Please call Spindi");
            }
        }

        public static void CalcRms2(BinaryReader br, WaveHeader wh, uint Step, uint AvgDurationFrames)
        {
            //uint NumberAvgValues = AvgDurationFrames / AvgNumberFrames;

            long LastTicks = DateTime.Now.Ticks;

            SpiBuffer FrameBufferSqr = new SpiBuffer(AvgDurationFrames);
            UInt64 FrameCount = 0;
            UInt64 StepCount = 0;
            double BiggestRms = 0;
            UInt64 BiggestRmsStart = 0;

            UInt32 ProgressCounter = 0;

            try
            {
                while (true)
                {
                    UInt16 RmsVal = WaveTools.CalcRmsValue(br, wh);
                    UInt64 SqrRms = (UInt64)RmsVal * (UInt64)RmsVal;

                    FrameBufferSqr.Add(SqrRms);
                    FrameCount++;
                    StepCount++;
                    ProgressCounter++;

                    // Wenn AvgNumberFrames erreicht wurden...
                    if (FrameBufferSqr.isFull && StepCount >= Step)
                    {
                        StepCount = 0;

                        // Den Durchschnittswert des SqrBuffers errechnen
                        double rms = Math.Sqrt((double)FrameBufferSqr.Sum / (double)AvgDurationFrames);

                        if (ProgressCounter >= wh.SampleRate * 60) // print progress every X seconds
                        {
                            ProgressCounter = 0;
                            Console.Write("Position: "
                                //+ WaveTools.FramesAsMinutesSeconds(FrameCount, wh.SampleRate) + " now: " + now + "\r");
                                + WaveTools.FramesAsMinutesSeconds(FrameCount, wh.SampleRate) + "\r");

                        }

                        if (rms > BiggestRms)
                        {
                            BiggestRms = rms;
                            BiggestRmsStart = FrameCount - AvgDurationFrames;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!e.Message.Equals("EOF"))
                {
                    Console.WriteLine(e);
                    throw (e);
                }
            }

            Console.WriteLine("{1}{0}", 
                (double)BiggestRms / Int16.MaxValue,
                WaveTools.PrintWithDots("Biggest Rms"));

            Console.WriteLine("{2}{0} ({1})",
                WaveTools.FramesAsSeconds(BiggestRmsStart, wh.SampleRate).ToString("0.000"),
                WaveTools.FramesAsMinutesSeconds(BiggestRmsStart, wh.SampleRate),
                WaveTools.PrintWithDots("Start at"));

        }
        /*
        public static void CalcRms(BinaryReader br, WaveHeader wh, uint AvgNumberFrames, uint AvgDurationFrames)
        {
            uint NumberAvgValues = AvgDurationFrames / AvgNumberFrames;

            SpiBuffer AvgBuffer = new SpiBuffer(NumberAvgValues);
            SpiBuffer Frame     = new SpiBuffer(AvgNumberFrames);

            UInt64 FrameCount = 0;
            double BiggestAvg = 0;
            UInt64 BiggestAvgStart = 0;

            Console.WriteLine("Schrittweite ......... " + WaveTools.FramesAsSeconds(AvgNumberFrames,wh.SampleRate).ToString("0.000") + "s");
            Console.WriteLine("Zeitspanne ........... " + WaveTools.FramesAsSeconds(AvgDurationFrames,wh.SampleRate).ToString("0.000") + "s");

            //TextWriter tw = new StreamWriter("rms.txt");

            try
            {
                while (true)
                {
                    UInt16 RmsVal = WaveTools.CalcRmsValue(br, wh);

                    UInt64 SqrRms = (UInt64)RmsVal * (UInt64)RmsVal;

                    Frame.Add(SqrRms);
                    FrameCount++;

                    // Wenn AvgNumberFrames erreicht wurden...
                    if (Frame.isFull)
                    {
                        // ... dann den Durchschnittswert des Buffers in den AvgValues speichern
                        double rms = Math.Sqrt( (double)Frame.Sum / (double)AvgNumberFrames);
                        AvgBuffer.Add( (UInt64)(rms) );

                        Frame.Reset();

                        // wenn die AvgValues befüllt sind, dann können wir den Durchschnitt dieser berechnen
                        if (AvgBuffer.isFull)
                        {
                            double Avg = (double)AvgBuffer.Sum / (double)NumberAvgValues;
                            
                            Console.WriteLine("Sek {0}\t bis\t {1}\t Value: {2}", 
                                WaveTools.FramesAsSeconds( FrameCount - AvgDurationFrames,wh.SampleRate).ToString("000.00"),
                                WaveTools.FramesAsSeconds( FrameCount, wh.SampleRate).ToString("000.00"),
                                Avg / (double)Int16.MaxValue);

                            if (Avg > BiggestAvg)
                            {
                                BiggestAvg = Avg;
                                BiggestAvgStart = FrameCount - AvgDurationFrames;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!e.Message.Equals("EOF"))
                {
                    Console.WriteLine(e);
                    throw (e);
                }
            }
            
            Console.WriteLine("Der Wert ... {0}", (float)BiggestAvg / Int16.MaxValue);
            Console.WriteLine("Start ...... {0} ({1})", 
                WaveTools.FramesAsSeconds(BiggestAvgStart, wh.SampleRate).ToString("0.000"),
                WaveTools.FramesAsMinutesSeconds(BiggestAvgStart, wh.SampleRate));


            //tw.Close();
        }
         */
    }
}
