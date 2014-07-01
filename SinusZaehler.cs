using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace WaveWork
{
    class SinusZaehler
    {
        public static void CalcHz(BinaryReader br, WaveHeader2 wh)
        {
            byte[] value = new byte[2];
            Int16 Sample;
            UInt64 SchwingungGesamt = 0;
            UInt64 FramesGesamt = 0;
            ulong SchwingungProSekunde = 0;
            long Sekunde = 1;
            Int16 Lastvalue;
            int PassedLine = 0;
            double Durchschnitt = 0;
            int CurrFrame = 0;

            try
            {
                Lastvalue = WaveTools.GetSampleValue(br.ReadBytes(2));

                while (true)
                {
                    value = br.ReadBytes(2);
                    if (value.Length < 2) break;
                    Sample = WaveTools.GetSampleValue(value);

                    if ((Lastvalue <= 0 && Sample > 0) || (Lastvalue >= 0 && Sample < 0))
                    {
                        PassedLine++;
                    }

                    if (PassedLine == 3)
                    {
                        SchwingungProSekunde++;
                        SchwingungGesamt++;
                        PassedLine = 1;
                    }

                    // Eine weitere Sekunde ging ins Land...
                    if (CurrFrame >= wh.SampleRate)
                    {
                        Console.WriteLine("Schwingungen in Sekunde >" + Sekunde.ToString("000") + "<: " + SchwingungProSekunde);
                        SchwingungProSekunde = 0;
                        CurrFrame = 0;
                        Sekunde++;
                    }
                    Lastvalue = Sample;
                    CurrFrame++;
                    FramesGesamt++;
                }
                Durchschnitt = (double)SchwingungGesamt / ((double)FramesGesamt / (double)wh.SampleRate);
                WaveTools.PrintWithDots("Durchschnitt", Durchschnitt + " Hz");
                WaveTools.PrintWithDots("Schwingungen Gesamt", SchwingungGesamt.ToString());
                WaveTools.PrintWithDots("Frames Gesamt", FramesGesamt.ToString());

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
