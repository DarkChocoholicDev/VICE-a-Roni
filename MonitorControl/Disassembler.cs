using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using VICEBinaryMonitor;

namespace MonitorControl
{
    public enum AddressingMode
    {
        Implied = 0,
        Accumulator,
        Immediate,
        ZeroPage,
        ZeroPageX,
        ZeroPageY,
        Relative,
        ZeroPageXIndirect,
        ZeroPageIndirectY,
        Absolute,
        AbsoluteX,
        AbsoluteY,
        Indirect,
    }


    public enum SupportLevel
    {
        Official,
        UndocumentedButStable,
        UndocumentedAndUnstable,
    }

    public class Instruction
    {
        public byte Opcode { get; set; }
        public required string Mnemonic { get; set; }
        public byte Length { get; set; }
        public AddressingMode AddressingMode { get; set; } = AddressingMode.Implied;

        public SupportLevel SupportLevel { get; set; } = SupportLevel.Official;
        public bool IsOfficial => SupportLevel == SupportLevel.Official;
        public bool IsStable => SupportLevel != SupportLevel.UndocumentedAndUnstable;

        [SetsRequiredMembers]
        public Instruction(byte opcode, string mnemonic, byte length, AddressingMode addressingMode, SupportLevel supportLevel = SupportLevel.Official)
        {
            Opcode = opcode;
            Mnemonic = mnemonic;
            Length = length;
            AddressingMode = addressingMode;
            SupportLevel = supportLevel;
        }

        public static Instruction[] Instructions { get; set; } = new Instruction[256]
        {
            new(0x00, "BRK", 1, AddressingMode.Implied),
            new(0x01, "ORA", 2, AddressingMode.ZeroPageXIndirect),
            new(0x02, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x03, "SLO", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0x04, "NOP", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x05, "ORA", 2, AddressingMode.ZeroPage),
            new(0x06, "ASL", 2, AddressingMode.ZeroPage),
            new(0x07, "SLO", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x08, "PHP", 1, AddressingMode.Implied),
            new(0x09, "ORA", 2, AddressingMode.Immediate),
            new(0x0A, "ASL", 1, AddressingMode.Accumulator),
            new(0x0B, "ANC", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0x0C, "NOP", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),
            new(0x0D, "ORA", 3, AddressingMode.Absolute),
            new(0x0E, "ASL", 3, AddressingMode.Absolute),
            new(0x0F, "SLO", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0x10, "BPL", 2, AddressingMode.Relative),
            new(0x11, "ORA", 2, AddressingMode.ZeroPageIndirectY),
            new(0x12, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x13, "SLO", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedButStable),
            new(0x14, "NOP", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x15, "ORA", 2, AddressingMode.ZeroPageX),
            new(0x16, "ASL", 2, AddressingMode.ZeroPageX),
            new(0x17, "SLO", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x18, "CLC", 1, AddressingMode.Implied),
            new(0x19, "ORA", 3, AddressingMode.AbsoluteY),
            new(0x1A, "NOP", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x1B, "SLO", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),
            new(0x1C, "NOP", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),
            new(0x1D, "ORA", 3, AddressingMode.AbsoluteX),
            new(0x1E, "ASL", 3, AddressingMode.AbsoluteX),
            new(0x1F, "SLO", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),

            new(0x20, "JSR", 3, AddressingMode.Absolute),
            new(0x21, "AND", 2, AddressingMode.ZeroPageXIndirect),
            new(0x22, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x23, "RLA", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0x24, "BIT", 2, AddressingMode.ZeroPage),
            new(0x25, "AND", 2, AddressingMode.ZeroPage),
            new(0x26, "ROL", 2, AddressingMode.ZeroPage),
            new(0x27, "RLA", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x28, "PLP", 1, AddressingMode.Implied),
            new(0x29, "AND", 2, AddressingMode.Immediate),
            new(0x2A, "ROL", 1, AddressingMode.Accumulator),
            new(0x2B, "ANC", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0x2C, "BIT", 3, AddressingMode.Absolute),
            new(0x2D, "AND", 3, AddressingMode.Absolute),
            new(0x2E, "ROL", 3, AddressingMode.Absolute),
            new(0x2F, "RLA", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0x30, "BMI", 2, AddressingMode.Relative),
            new(0x31, "AND", 2, AddressingMode.ZeroPageIndirectY),
            new(0x32, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x33, "RLA", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedButStable),
            new(0x34, "NOP", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x35, "AND", 2, AddressingMode.ZeroPageX),
            new(0x36, "ROL", 2, AddressingMode.ZeroPageX),
            new(0x37, "RLA", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x38, "SEC", 1, AddressingMode.Implied),
            new(0x39, "AND", 3, AddressingMode.AbsoluteY),
            new(0x3A, "NOP", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x3B, "RLA", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),
            new(0x3C, "NOP", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),
            new(0x3D, "AND", 3, AddressingMode.AbsoluteX),
            new(0x3E, "ROL", 3, AddressingMode.AbsoluteX),
            new(0x3F, "RLA", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),

            new(0x40, "RTI", 1, AddressingMode.Implied),
            new(0x41, "EOR", 2, AddressingMode.ZeroPageXIndirect),
            new(0x42, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x43, "SRE", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0x44, "NOP", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x45, "EOR", 2, AddressingMode.ZeroPage),
            new(0x46, "LSR", 2, AddressingMode.ZeroPage),
            new(0x47, "SRE", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x48, "PHA", 1, AddressingMode.Implied),
            new(0x49, "EOR", 2, AddressingMode.Immediate),
            new(0x4A, "LSR", 1, AddressingMode.Accumulator),
            new(0x4B, "ALR", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0x4C, "JMP", 3, AddressingMode.Absolute),
            new(0x4D, "EOR", 3, AddressingMode.Absolute),
            new(0x4E, "LSR", 3, AddressingMode.Absolute),
            new(0x4F, "SRE", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0x50, "BVC", 2, AddressingMode.Relative),
            new(0x51, "EOR", 2, AddressingMode.ZeroPageIndirectY),
            new(0x52, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x53, "SRE", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedButStable),
            new(0x54, "NOP", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x55, "EOR", 2, AddressingMode.ZeroPageX),
            new(0x56, "LSR", 2, AddressingMode.ZeroPageX),
            new(0x57, "SRE", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x58, "CLI", 1, AddressingMode.Implied),
            new(0x59, "EOR", 3, AddressingMode.AbsoluteY),
            new(0x5A, "NOP", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x5B, "SRE", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),
            new(0x5C, "NOP", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),
            new(0x5D, "EOR", 3, AddressingMode.AbsoluteX),
            new(0x5E, "LSR", 3, AddressingMode.AbsoluteX),
            new(0x5F, "SRE", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),

            new(0x60, "RTS", 1, AddressingMode.Implied),
            new(0x61, "ADC", 2, AddressingMode.ZeroPageXIndirect),
            new(0x62, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x63, "RRA", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0x64, "NOP", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x65, "ADC", 2, AddressingMode.ZeroPage),
            new(0x66, "ROR", 2, AddressingMode.ZeroPage),
            new(0x67, "RRA", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x68, "PLA", 1, AddressingMode.Implied),
            new(0x69, "ADC", 2, AddressingMode.Immediate),
            new(0x6A, "ROR", 1, AddressingMode.Accumulator),
            new(0x6B, "ARR", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0x6C, "JMP", 3, AddressingMode.Indirect),
            new(0x6D, "ADC", 3, AddressingMode.Absolute),
            new(0x6E, "ROR", 3, AddressingMode.Absolute),
            new(0x6F, "RRA", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0x70, "BVS", 2, AddressingMode.Relative),
            new(0x71, "ADC", 2, AddressingMode.ZeroPageIndirectY),
            new(0x72, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x73, "RRA", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedButStable),
            new(0x74, "NOP", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x75, "ADC", 2, AddressingMode.ZeroPageX),
            new(0x76, "ROR", 2, AddressingMode.ZeroPageX),
            new(0x77, "RRA", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0x78, "SEI", 1, AddressingMode.Implied),
            new(0x79, "ADC", 3, AddressingMode.AbsoluteY),
            new(0x7A, "NOP", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x7B, "RRA", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),
            new(0x7C, "NOP", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),
            new(0x7D, "ADC", 3, AddressingMode.AbsoluteX),
            new(0x7E, "ROR", 3, AddressingMode.AbsoluteX),
            new(0x7F, "RRA", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),

            new(0x80, "NOP", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0x81, "STA", 2, AddressingMode.ZeroPageXIndirect),
            new(0x82, "NOP", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0x83, "SAX", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0x84, "STY", 2, AddressingMode.ZeroPage),
            new(0x85, "STA", 2, AddressingMode.ZeroPage),
            new(0x86, "STX", 2, AddressingMode.ZeroPage),
            new(0x87, "SAX", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0x88, "DEY", 1, AddressingMode.Implied),
            new(0x89, "NOP", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0x8A, "TXA", 1, AddressingMode.Implied),
            new(0x8B, "XAA", 2, AddressingMode.Immediate, SupportLevel.UndocumentedAndUnstable),
            new(0x8C, "STY", 3, AddressingMode.Absolute),
            new(0x8D, "STA", 3, AddressingMode.Absolute),
            new(0x8E, "STX", 3, AddressingMode.Absolute),
            new(0x8F, "SAX", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0x90, "BCC", 2, AddressingMode.Relative),
            new(0x91, "STA", 2, AddressingMode.ZeroPageIndirectY),
            new(0x92, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0x93, "AHX", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedAndUnstable),
            new(0x94, "STY", 2, AddressingMode.ZeroPageX),
            new(0x95, "STA", 2, AddressingMode.ZeroPageX),
            new(0x96, "STX", 2, AddressingMode.ZeroPageY),
            new(0x97, "SAX", 2, AddressingMode.ZeroPageY, SupportLevel.UndocumentedButStable),
            new(0x98, "TYA", 1, AddressingMode.Implied),
            new(0x99, "STA", 3, AddressingMode.AbsoluteY),
            new(0x9A, "TXS", 1, AddressingMode.Implied),
            new(0x9B, "TAS", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedAndUnstable),
            new(0x9C, "SHY", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedAndUnstable),
            new(0x9D, "STA", 3, AddressingMode.AbsoluteX),
            new(0x9E, "SHX", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedAndUnstable),
            new(0x9F, "AHX", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedAndUnstable),

            new(0xA0, "LDY", 2, AddressingMode.Immediate),
            new(0xA1, "LDA", 2, AddressingMode.ZeroPageXIndirect),
            new(0xA2, "LDX", 2, AddressingMode.Immediate),
            new(0xA3, "LAX", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0xA4, "LDY", 2, AddressingMode.ZeroPage),
            new(0xA5, "LDA", 2, AddressingMode.ZeroPage),
            new(0xA6, "LDX", 2, AddressingMode.ZeroPage),
            new(0xA7, "LAX", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0xA8, "TAY", 1, AddressingMode.Implied),
            new(0xA9, "LDA", 2, AddressingMode.Immediate),
            new(0xAA, "TAX", 1, AddressingMode.Implied),
            new(0xAB, "LAX", 2, AddressingMode.Immediate, SupportLevel.UndocumentedAndUnstable),
            new(0xAC, "LDY", 3, AddressingMode.Absolute),
            new(0xAD, "LDA", 3, AddressingMode.Absolute),
            new(0xAE, "LDX", 3, AddressingMode.Absolute),
            new(0xAF, "LAX", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0xB0, "BCS", 2, AddressingMode.Relative),
            new(0xB1, "LDA", 2, AddressingMode.ZeroPageIndirectY),
            new(0xB2, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0xB3, "LAX", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedButStable),
            new(0xB4, "LDY", 2, AddressingMode.ZeroPageX),
            new(0xB5, "LDA", 2, AddressingMode.ZeroPageX),
            new(0xB6, "LDX", 2, AddressingMode.ZeroPageY),
            new(0xB7, "LAX", 2, AddressingMode.ZeroPageY, SupportLevel.UndocumentedButStable),
            new(0xB8, "CLV", 1, AddressingMode.Implied),
            new(0xB9, "LDA", 3, AddressingMode.AbsoluteY),
            new(0xBA, "TSX", 1, AddressingMode.Implied),
            new(0xBB, "LAS", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),
            new(0xBC, "LDY", 3, AddressingMode.AbsoluteX),
            new(0xBD, "LDA", 3, AddressingMode.AbsoluteX),
            new(0xBE, "LDX", 3, AddressingMode.AbsoluteY),
            new(0xBF, "LAX", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),

            new(0xC0, "CPY", 2, AddressingMode.Immediate),
            new(0xC1, "CMP", 2, AddressingMode.ZeroPageXIndirect),
            new(0xC2, "NOP", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0xC3, "DCP", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0xC4, "CPY", 2, AddressingMode.ZeroPage),
            new(0xC5, "CMP", 2, AddressingMode.ZeroPage),
            new(0xC6, "DEC", 2, AddressingMode.ZeroPage),
            new(0xC7, "DCP", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0xC8, "INY", 1, AddressingMode.Implied),
            new(0xC9, "CMP", 2, AddressingMode.Immediate),
            new(0xCA, "DEX", 1, AddressingMode.Implied),
            new(0xCB, "AXS", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0xCC, "CPY", 3, AddressingMode.Absolute),
            new(0xCD, "CMP", 3, AddressingMode.Absolute),
            new(0xCE, "DEC", 3, AddressingMode.Absolute),
            new(0xCF, "DCP", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0xD0, "BNE", 2, AddressingMode.Relative),
            new(0xD1, "CMP", 2, AddressingMode.ZeroPageIndirectY),
            new(0xD2, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0xD3, "DCP", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedButStable),
            new(0xD4, "NOP", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0xD5, "CMP", 2, AddressingMode.ZeroPageX),
            new(0xD6, "DEC", 2, AddressingMode.ZeroPageX),
            new(0xD7, "DCP", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0xD8, "CLD", 1, AddressingMode.Implied),
            new(0xD9, "CMP", 3, AddressingMode.AbsoluteY),
            new(0xDA, "NOP", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0xDB, "DCP", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),
            new(0xDC, "NOP", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),
            new(0xDD, "CMP", 3, AddressingMode.AbsoluteX),
            new(0xDE, "DEC", 3, AddressingMode.AbsoluteX),
            new(0xDF, "DCP", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),

            new(0xE0, "CPX", 2, AddressingMode.Immediate),
            new(0xE1, "SBC", 2, AddressingMode.ZeroPageXIndirect),
            new(0xE2, "NOP", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0xE3, "ISC", 2, AddressingMode.ZeroPageXIndirect, SupportLevel.UndocumentedButStable),
            new(0xE4, "CPX", 2, AddressingMode.ZeroPage),
            new(0xE5, "SBC", 2, AddressingMode.ZeroPage),
            new(0xE6, "INC", 2, AddressingMode.ZeroPage),
            new(0xE7, "ISC", 2, AddressingMode.ZeroPage, SupportLevel.UndocumentedButStable),
            new(0xE8, "INX", 1, AddressingMode.Implied),
            new(0xE9, "SBC", 2, AddressingMode.Immediate),
            new(0xEA, "NOP", 1, AddressingMode.Implied),
            new(0xEB, "SBC", 2, AddressingMode.Immediate, SupportLevel.UndocumentedButStable),
            new(0xEC, "CPX", 3, AddressingMode.Absolute),
            new(0xED, "SBC", 3, AddressingMode.Absolute),
            new(0xEE, "INC", 3, AddressingMode.Absolute),
            new(0xEF, "ISC", 3, AddressingMode.Absolute, SupportLevel.UndocumentedButStable),

            new(0xF0, "BEQ", 2, AddressingMode.Relative),
            new(0xF1, "SBC", 2, AddressingMode.ZeroPageIndirectY),
            new(0xF2, "KIL", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0xF3, "ISC", 2, AddressingMode.ZeroPageIndirectY, SupportLevel.UndocumentedButStable),
            new(0xF4, "NOP", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0xF5, "SBC", 2, AddressingMode.ZeroPageX),
            new(0xF6, "INC", 2, AddressingMode.ZeroPageX),
            new(0xF7, "ISC", 2, AddressingMode.ZeroPageX, SupportLevel.UndocumentedButStable),
            new(0xF8, "SED", 1, AddressingMode.Implied),
            new(0xF9, "SBC", 3, AddressingMode.AbsoluteY),
            new(0xFA, "NOP", 1, AddressingMode.Implied, SupportLevel.UndocumentedButStable),
            new(0xFB, "ISC", 3, AddressingMode.AbsoluteY, SupportLevel.UndocumentedButStable),
            new(0xFC, "NOP", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),
            new(0xFD, "SBC", 3, AddressingMode.AbsoluteX),
            new(0xFE, "INC", 3, AddressingMode.AbsoluteX),
            new(0xFF, "ISC", 3, AddressingMode.AbsoluteX, SupportLevel.UndocumentedButStable),
        };
    }

    public class Disassembler
    {
        public string DisassembleSingle(UInt16 instructionAddress, MemoryRegion region, SourceSymbolTable symbolTable, out UInt16 nextInstructionAddress)
        {
            string disassembly;
            byte[]? instructionBytes = null;
            string? operand = null;

            // If we were able to read the opcode byte...
            if (region.GetByte(instructionAddress, out var opcodeByte))
            {
                var instruction = Instruction.Instructions[opcodeByte];
                nextInstructionAddress = (UInt16)(instructionAddress + instruction.Length);
                {
                    instructionBytes = region.GetBytes(instructionAddress, instruction.Length);
                    if (instructionBytes == null)
                    {
                        operand = "...partial instruction...";
                    }
                    else
                    {
                        operand = GetOperand(instruction, instructionAddress, region, symbolTable);
                    }
                }

                //if (instructionBytes != null)
                {
                    disassembly = string.Format(
                        $"{instructionAddress:X4}  " +
                        $"{opcodeByte:X2}" +
                        ((instructionBytes?.Length >= 2) ? $" {instructionBytes[1]:X2}" : "   ") +
                        ((instructionBytes?.Length >= 3) ? $" {instructionBytes[2]:X2}" : "   ") +
                        $"  {instruction.Mnemonic}{(operand != null ? $" {operand}" : "")}");
                }
                //else
                //{
                //    $"{instructionAddress:X4}  " +
                //    $"{opcodeByte:X2}" +
                //    ((instructionBytes!.Length >= 2) ? $" {instructionBytes[1]:X2}" : "   ") +
                //    ((instructionBytes!.Length >= 3) ? $" {instructionBytes[2]:X2}" : "   ") +
                //    $"  {instruction.Mnemonic}{(operand != null ? $" {operand}" : "")}");
                //}

            }
            else
            {
                // Return an error string if we failed.
                disassembly = string.Format($"{instructionAddress:X4}:    ?");
                nextInstructionAddress = (UInt16)(instructionAddress + 1);
            }

            return disassembly;
        }

        private string? GetOperand(Instruction instruction, UInt16 instructionAddress, MemoryRegion region, SourceSymbolTable symbolTable)
        {
            byte byteOperand = 0;
            UInt16 wordOperand = 0;
            UInt16 operandAddress = (UInt16)(instructionAddress + 1);
            var operandRead  = true;

            if (instruction.Length == 2)
            {
                operandRead = region.GetByte(operandAddress, out byteOperand);
            }
            else if (instruction.Length == 3)
            {
                operandRead = region.GetWord(operandAddress, out wordOperand);
            }

            if (operandRead)
            {
                switch (instruction.AddressingMode)
                {
                    case AddressingMode.Implied:
                        return null;

                    case AddressingMode.Accumulator:
                        return "A";

                    case AddressingMode.Immediate:
                        return $"#{byteOperand:X2}";

                    case AddressingMode.ZeroPage:
                        return LabelOrZeroPageAddress(byteOperand, symbolTable);

                    case AddressingMode.ZeroPageX:
                        return $"{LabelOrZeroPageAddress(byteOperand, symbolTable)},X";

                    case AddressingMode.ZeroPageY:
                        return $"{LabelOrZeroPageAddress(byteOperand, symbolTable)},Y";

                    case AddressingMode.Relative:
                        UInt16 offset = (UInt16)((sbyte)(byteOperand));
                        UInt16 target = (UInt16)(instructionAddress + 2 + offset);
                        return NearestLabelOrAddress(target, symbolTable);

                    case AddressingMode.ZeroPageXIndirect:
                        return $"({LabelOrZeroPageAddress(byteOperand, symbolTable)},X)";

                    case AddressingMode.ZeroPageIndirectY:
                        return $"({LabelOrZeroPageAddress(byteOperand, symbolTable)}),Y";

                    case AddressingMode.Absolute:
                        return LabelOrAddress(wordOperand, symbolTable);

                    case AddressingMode.AbsoluteX:
                        return $"{LabelOrAddress(wordOperand, symbolTable)},X";

                    case AddressingMode.AbsoluteY:
                        return $"{LabelOrAddress(wordOperand, symbolTable)},Y";

                    case AddressingMode.Indirect:
                        return $"({LabelOrAddress(wordOperand, symbolTable)})";

                    default:
                        return "...unsupported addressing mode...";
                }
            }
            else
            {
                return "...incomplete instruction...";
            }
        }

        private string LabelOrAddress(UInt16 address, SourceSymbolTable symbolTable)
        {
            var firstMatch = symbolTable.GetSymbolsFromValue(address).FirstOrDefault();
            if (firstMatch != null)
            {
                return $"{firstMatch.Name}";
            }
            else
            {
                return $"{address:X4}";
            }
        }

        private string LabelOrZeroPageAddress(byte address, SourceSymbolTable symbolTable)
        {

            var firstMatch = symbolTable.GetSymbolsFromValue(address).FirstOrDefault();
            if (firstMatch != null)
            {
                return $"{firstMatch.Name}";
            }
            else
            {
                return $"{address:X2}";
            }
        }

        private string NearestLabelOrAddress(UInt16 address, SourceSymbolTable symbolTable, int thresholdDistance = 255)
        {
            var firstMatch = symbolTable.GetSymbolsFromValue(address).FirstOrDefault();
            if (firstMatch != null)
            {
                return $"{firstMatch.Name}";
            }
            else
            {
                firstMatch = symbolTable.Symbols.Where(s => s.Value.Value < address && s.Value.Value > address - thresholdDistance).OrderByDescending(s => s.Value.Value).FirstOrDefault()!.Value;
                if (firstMatch != null)
                {
                    var delta = address - firstMatch.Value;
                    return $"{address:X4} ({firstMatch.Name}+{delta:X2})";
                }
                else
                {
                    return $"{address:X4}";
                }
            }
        }
    }

}
