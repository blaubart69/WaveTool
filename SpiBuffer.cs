using System;
using System.Collections.Generic;
using System.Text;

namespace WaveWork
{
    public class SpiBuffer
    {
        private UInt64[] Buf;
        private uint CurrPos;
        //private uint LastPos;
        public uint size = 0;
        public UInt64 Sum = 0;
        public bool isFull = false;

        public SpiBuffer(uint size)
        {
            Buf = new UInt64[size];
            this.size = size;
            Reset();
        }
        public void Reset()
        {
            isFull = false;
            CurrPos = 0;
            //LastPos = 0;
            Sum = 0;
            for (int i = 0; i < size; i++)
                Buf[i] = 0;
        }
        public void Add(UInt64 val)
        {
            Sum -= Buf[CurrPos];

            Buf[CurrPos] = val;
            CurrPos++;

            Sum += val;

            if (CurrPos >= size)
            {
                CurrPos = 0;
                isFull = true;
            }

        }
    }
}
