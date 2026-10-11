using System;
using System.Collections.Generic;
using System.Text;

namespace MonitorControl
{
    public class Settings
    {
        public string[] SymbolsFiles { get; set; } = [];
        public uint DefaultDumpLength { get; set; } = 128;
    }
}
