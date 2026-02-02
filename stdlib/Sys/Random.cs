using System;

namespace StarChart.stdlib.Sys
{
    public static class Random
    {
        private static System.Random _rnd = new System.Random();

        public static int Next()
        {
            return _rnd.Next();
        }

        public static int Next(int max)
        {
            return _rnd.Next(max);
        }

        public static int Next(int min, int max)
        {
            return _rnd.Next(min, max);
        }

        public static double NextDouble()
        {
            return _rnd.NextDouble();
        }
        
        public static float NextFloat() // Common in game dev
        {
             return (float)_rnd.NextDouble();
        }
    }
}
