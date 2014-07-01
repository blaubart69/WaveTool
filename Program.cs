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


            Int16 i = (Int16)( (Int16)(a << 8) | (Int16)b );

            //Int16 i6 = ( (Int16)a << (Int16)8 );

        }

        public static void PrintUsage()
        {
            Console.WriteLine(
              "Usage: WaveTool -file {Wavefilename} {-Rms|CountHz} [-RmsWindow {RmsWindowsInFrames}]\n"
            + "Rms-Params:  -RmsWindow [number]... a number of frames\n"
            + "Rms example: >>>WaveTool.Exe -rms -file test.wav -RmsWindow 441000<<<\n");

        }

        private static int ParamCheck(at.spi.Tools.CmdArgs Opts)
        {
            string MissingParam;
            if (!Opts.CheckMustParams(new string[] { "file" }, out MissingParam))
            {
                PrintUsage();
                return 4;
            }

            if ( !Opts.exists("rms") && !Opts.exists("counthz") )
            {
                Console.WriteLine("E: you must specifiy [-rms] or [-CountHz] as an action");
                PrintUsage();
                return 8;
            }

            return 0;
        }

        static int Main(string[] args)
        {
            testo();

            int rc = 8;
            at.spi.Tools.CmdArgs Opts = new at.spi.Tools.CmdArgs(args);
            if ((rc = ParamCheck(Opts)) != 0)
            {
                return rc;
            }

            Stream fs;
            fs = new FileStream(Opts.GetString("file"), FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);

            try
            {
                long StartTicks = DateTime.Now.Ticks;

                WaveHeader2 wh = WaveTools.ReadHeader(br);
                WaveTools.PrintHeader(wh);

                if (Opts.exists("counthz"))
                {
                    SinusZaehler.CalcHz(br, wh);
                }
                else if (Opts.exists("rms"))
                {
                    uint RmsWindow;
                    Opts.GetUInt("RmsWindow",out RmsWindow, wh.SampleRate * 10 );  // a 10s window
                    WaveTools.PrintWithDots("RmsWindow", WaveTools.FramesAsSeconds(RmsWindow, wh.SampleRate).ToString("0.000") + "s");
                    Rms.CalcRms3(br, wh, RmsWindow);
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
            return rc;
        }
    }
}
