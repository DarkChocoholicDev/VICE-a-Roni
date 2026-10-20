using System;
using System.Collections.Generic;
using System.Text;

namespace MonitorControl
{
    /// <summary>
    /// A class representing a snapshot of the 6502's CPU registers.
    /// </summary>
    public class Registers6502
    {
        /// <summary>
        /// Gets or sets the value of the A register.
        /// </summary>
        public byte A { get; set; }

        /// <summary>
        /// Gets or sets the value of the X register.
        /// </summary>
        public byte X { get; set; }

        /// <summary>
        /// Gets or sets the value of the Y register.
        /// </summary>
        public byte Y { get; set; }

        /// <summary>
        /// Gets or sets the value of the stack pointer.
        /// </summary>
        public byte SP { get; set; }

        /// <summary>
        /// Gets or sets the value of the program counter.
        /// </summary>
        public UInt16 PC { get; set; }

        /// <summary>
        /// Gets or sets the value of the processor status (flags) register.
        /// </summary>
        public byte Flags { get; set; }

        /// <summary>
        /// Gets the set of processor status flags as a single string.
        /// </summary>
        public string FlagString =>
            $"{(Negative ? 'N' : "-")}" +
            $"{(Overflow ? 'V' : "-")}" +
            $"{(Break ? 'B' : "-")}" +
            $"{(DecimalMode ? 'D' : "-")}" +
            $"{(InterruptDisable ? 'I' : "-")}" +
            $"{(Zero ? 'Z' : "-")}" +
            $"{(Carry ? 'C' : "-")}";

        /// <summary>
        /// Gets the value of the Negative bit of the processor status register.
        /// </summary>
        public bool Negative => (Flags & 0b10000000) != 0;

        /// <summary>
        /// Gets the value of the Overflow bit of the processor status register.
        /// </summary>
        public bool Overflow => (Flags & 0b01000000) != 0;

        /// <summary>
        /// Gets the value of the Break bit of the processor status register.
        /// </summary>
        public bool Break => (Flags & 0b00010000) != 0;

        /// <summary>
        /// Gets the value of the Decimal Mode bit of the processor status register.
        /// </summary>
        public bool DecimalMode => (Flags & 0b00001000) != 0;

        /// <summary>
        /// Gets the value of the Interrupt Disable bit of the processor status register.
        /// </summary>
        public bool InterruptDisable => (Flags & 0b00000100) != 0;

        /// <summary>
        /// Gets the value of the Zero bit of the processor status register.
        /// </summary>
        public bool Zero => (Flags & 0b00000010) != 0;

        /// <summary>
        /// Gets the value of the Carry bit of the processor status register.
        /// </summary>
        public bool Carry => (Flags & 0b00000001) != 0;
    }
}
