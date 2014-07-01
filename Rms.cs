using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WaveWork
{
    public class Rms
    {
        private UInt64 BytesProcessed = 0;
        private ulong FramePos = 0;

        private WaveHeader2 WavHeader;

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
            Console.WriteLine("----- RESULTS (start) -----");

            Console.WriteLine("{0}{1}", WaveTools.PrintWithDots("Biggest Rms"), BiggestRms);

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

            Console.WriteLine("----- RESULTS (end) -------");
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
        
        private static UInt64 GetAverageValueSqr(ushort Channels, byte[] Data, int pos, out ushort BytesProcessed)
        {
            UInt64 SqrRms;

            if (Channels == 1)
            {
                UInt64 AvgVal;
                Int16 SampleValue = BitConverter.ToInt16(Data, pos);
                AvgVal = (UInt64)Math.Abs(SampleValue);
                SqrRms = AvgVal * AvgVal;
                BytesProcessed = 2;
            }
            else //if (Channels == 2)
            {
                UInt64 AvgVal;
                Int16 LeftChannel  = BitConverter.ToInt16(Data, pos);
                Int16 RightChannel = BitConverter.ToInt16(Data, pos + 2);

                UInt64 AbsLeft = (UInt64)Math.Abs(LeftChannel);
                UInt64 AbsRight = (UInt64)Math.Abs(RightChannel);

                AvgVal = AbsLeft * AbsLeft + AbsRight * AbsRight;
                SqrRms = AvgVal / 2;
                BytesProcessed = 4;
            }

            return SqrRms;
        }

        public void CalcRms3(BinaryReader br, WaveHeader2 wh, uint FrameWindow)
        {
            const   int     BufferSize = 16 * 1024 * 1000;
            byte[] Data;

            this.WavHeader = wh;    // using this to print the stats via the timer

            SpiBuffer FrameBufferSqr = new SpiBuffer(FrameWindow);

            UInt64 BiggestRms_SqrSum = 0;
            UInt64 BiggestRms_SqrSum_StartPos = 0;

            System.Timers.Timer timer = new System.Timers.Timer(1000);
            timer.Elapsed += new System.Timers.ElapsedEventHandler(Progress_Timer_Elapsed);
            timer.Start();

            DateTime StartTime = DateTime.Now;

            this.FramePos = 0;
            while ( (Data = br.ReadBytes(BufferSize)).Length != 0  )
            {
                int pos=0;
                while ( pos < Data.Length )
                {
                    ushort BytesRead;
                    UInt64 SqrRms = GetAverageValueSqr(wh.Channels, Data, pos, out BytesRead);
                    pos += BytesRead;
                    this.BytesProcessed += BytesRead;

                    FrameBufferSqr.Add( SqrRms );

                    if (FrameBufferSqr.isFull)
                    {
                        // 
                        // 2014-07-01 Spindler
                        //  just store the biggest sum here and divide and SQRT afterwards. :-)
                        //
                        if (FrameBufferSqr.Sum > BiggestRms_SqrSum)
                        {
                            BiggestRms_SqrSum = FrameBufferSqr.Sum;
                            BiggestRms_SqrSum_StartPos = FramePos - FrameWindow;
                        }
                    }

                    /*
                    ProgressCounter++;
                    if (ProgressCounter >= wh.SampleRate * 60) // every "n" minute within the file
                    {
                        ProgressCounter = 0;
                        PrintProgressLine( BytesProcessed, wh.DataLength, FrameCount, wh.SampleRate);
                    }
                     * */
                    //
                    // move to the next frame
                    //
                    FramePos++;
                }
            }
            timer.Stop();
            timer.Close();

            if (BytesProcessed == wh.DataLength)
            {
                double Rms = Math.Sqrt((double)BiggestRms_SqrSum / (double)FrameWindow) / Int16.MaxValue;

                PrintRmsResult(Rms, BiggestRms_SqrSum_StartPos, wh.SampleRate, BytesProcessed, StartTime);
                Console.WriteLine("SUCCESS! (Bytes processed matches data length. WAV Header was read successfully.)");
            }
            else
            {
                Console.WriteLine("ERROR! The WAV File was no read successfully. Please call Spindi");
            }
        }

        private void Progress_Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            PrintProgressLine(this.BytesProcessed, WavHeader.DataLength, this.FramePos, WavHeader.SampleRate);
        }

        [Obsolete("CalcRms2 is deprecated. Use CalcRms3 instead.")]
        public void CalcRms2(BinaryReader br, WaveHeader2 wh, uint Step, uint AvgDurationFrames)
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
