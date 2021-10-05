using System;

namespace Application.Common.Models
{
    public class RateLimit
    {
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public TimeSpan TimeRemaining { get; set; }
    }
}