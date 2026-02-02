using System;

namespace StarChart.stdlib.Sys
{
    public static class Time
    {
        public static long GetTimestamp()
        {
            return DateTime.UtcNow.Ticks;
        }
        
        public static DateTime Now => DateTime.Now;
        public static DateTime UtcNow => DateTime.UtcNow;
    }
}
