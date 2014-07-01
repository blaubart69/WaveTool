using System;
using System.Collections.Generic;
using System.Text;

namespace WaveWork
{
    public static class Params
    {
        public static string Filename = "";
        public static string Action = "";
        public static uint FrameWindow = 0;

        public static void Init(string [] args)
        {
            FrameWindow = 44100 * 10;   // 10s

            for (int i = 0; i < args.Length; i++ )
            {
                switch (args[i].ToLower())
                {
                    case "-rms": Action = "rms"; break;
                    case "-counthz": Action = "counthz"; break;
                    case "-file": Filename = args[i + 1]; break;
                    case "-rmswindow": FrameWindow = Convert.ToUInt32(args[i + 1]); break;
                }
            }
        }
        public static void PrintUsage()
        {
            Console.WriteLine(
              "Usage: WaveTool -file {Wavefilename} {-Rms|CountHz} [-RmsWindows {RmsWindowsInFrames}]\n"
            + "Rms-Params:  -RmsWindow [number]... RmsWindow in frames\n"
            + "Rms example: >>>WaveTool.Exe -rms -file test.wav -step 22050 -RmsWindows 441000<<<\n");

        }
    }
}
