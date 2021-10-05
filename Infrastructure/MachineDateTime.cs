using Application.Common.Interfaces;
using System;

namespace Infrastructure
{
    public class MachineDateTime : IDateTime
    {
        public DateTime Now => DateTime.Now;
        public static int CurrentYear => DateTime.Now.Year;
    }
}