using System;
using System.IO;
using System.Collections.Generic;
using System.Text;


namespace WaveWork
{

    class Program
    {
        static void testo()
        {
            Int16 val = -240;

            byte a = (byte)(val >> 8);
            byte b = (byte)(val & 0x00FF);


            //Int16 i = (Int16)((UInt16)a << 8) | (UInt16)b;

            //Int16 i6 = ( (Int16)a << (Int16)8 );

        }

        static void Main(string[] args)
        {
            //testo();

            if (args.Length == 0)
            {
                Params.PrintUsage();
                return;
            }

            Params.Init(args);

            Stream fs;
            if (Params.Filename.Equals("-"))
            {
                fs = Console.OpenStandardInput();
            }
            else
            {
                fs = new FileStream(Params.Filename, FileMode.Open, FileAccess.Read);
            }
            BinaryReader br = new BinaryReader(fs);

            try
            {
                long StartTicks = DateTime.Now.Ticks;

                WaveHeader2 wh = WaveTools.ReadHeader(br);
                WaveTools.PrintHeader(wh, Params.Filename);

                if (Params.Action.Equals("counthz"))
                {
                    SinusZaehler.CalcHz(br, wh);
                }
                else if (Params.Action.Equals("rms"))
                {
                    WaveTools.PrintWithDots("Zeitspanne", WaveTools.FramesAsSeconds(Params.FrameWindow, wh.SampleRate).ToString("0.000") + "s");

                    Rms.CalcRms3(br, wh, Params.FrameWindow);
                }
                //Console.WriteLine("{0}{1}", WaveTools.PrintWithDots("Duration"),new TimeSpan(DateTime.Now.Ticks - StartTicks).ToString()); 
                WaveTools.PrintWithDots("Duration", new TimeSpan(DateTime.Now.Ticks - StartTicks).ToString());

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                fs.Close();
            }
        }
    }
}
