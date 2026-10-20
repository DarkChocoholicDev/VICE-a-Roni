using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using VICEBinaryMonitor;

namespace MonitorControl
{
    public enum AddressSpace
    {
        Main,
        Drive8,
        Drive9,
        Drive10,
        Drive11,
        COMALPage0,
        COMALPage1,
        COMALPage2,
        COMALPage3,
        COMALPage4,
        COMALPage5,
        COMALPage6,
    }

    /// <summary>
    /// A class representing a region of C64 memory.
    /// </summary>
    public class MemoryRegion
    {
        /// <summary>
        /// A semaphore for serializing access to memory accesses to ensure that address space changes
        /// don't get trampled.
        /// </summary>
        static SemaphoreSlim _accessSerializer = new (1,1);

        public MemoryRegion(UInt16 startingAddress, byte[] bytes, AddressSpace addressSpace = AddressSpace.Main)
        {
            Bytes = bytes;
            AddressSpace = addressSpace;
            StartingAddress = startingAddress;
        }

        /// <summary>
        /// Gets the collection of bytes in the memory range.
        /// </summary>
        public byte[] Bytes { get; init; }

        /// <summary>
        /// Gets the category of memory used for this region.
        /// </summary>
        public AddressSpace AddressSpace { get; set; }

        /// <summary>
        /// Gets the starting address of the memory range.
        /// </summary>
        public UInt16 StartingAddress
        {
            get
            {
                return _startingAddress;
            }
            init
            {
                _startingAddress = value;
                EndingAddress = (UInt16)(value + Bytes.Length - 1);
            }
        }
        UInt16 _startingAddress;

        /// <summary>
        /// Gets the last (highest) address in the memory region.
        /// </summary>
        public UInt16 EndingAddress { get; private set; }

        /// <summary>
        /// Gets the count of bytes in the memory region.
        /// </summary>
        public int Length => Bytes.Length;

        /// <summary>
        /// Gets the byte index of the specified address.
        /// </summary>
        /// <param name="address">Target memory address.</param>
        /// <param name="index">Receives the corresponding index, if successful.</param>
        /// <param name="bytesAvailable">Receives the count of bytes available beginning at the target memory address.</param>
        /// <returns>A value indicating whether the operation succeeded.</returns>
        /// <remarks>A failure could occur if the target address lies outside the range of the memory region.</remarks>
        public bool IndexFromAddress(UInt16 address, out int index, out int bytesAvailable)
        {
            index = address - StartingAddress;
            bytesAvailable = Length - index;
            return (index >= 0 && index < Length);
        }

        /// <summary>
        /// Gets a byte from the specified address.
        /// </summary>
        /// <param name="address">Target address.</param>
        /// <param name="value">Receive the byte value, if successful.</param>
        /// <returns>A value indicating whether the operation succeeded.</returns>
        public bool GetByte(UInt16 address, out byte value)
        {
            int index;
            int bytesAvailable;
            if (IndexFromAddress(address, out index, out bytesAvailable) && bytesAvailable >= 1)
            {
                value = Bytes[index];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public byte[]? GetBytes(UInt16 address, int byteCount)
        {
            if (IndexFromAddress(address, out var index, out var bytesAvailable) && bytesAvailable >= byteCount)
            {
                return Bytes[index..(index + byteCount)].ToArray();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a word from the specified address.
        /// </summary>
        /// <param name="address">Target address.</param>
        /// <param name="value">Receive the word value, if successful.</param>
        /// <returns>A value indicating whether the operation succeeded.</returns>
        public bool GetWord(UInt16 address, out UInt16 value)
        {
            int index;
            int bytesAvailable;
            if (IndexFromAddress(address, out index, out bytesAvailable) && bytesAvailable >= 2)
            {
                value = (UInt16)((Bytes[index + 1] << 8) | Bytes[index]);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Asynchronously reads from VICE memory using the specified address space.
        /// </summary>
        /// <param name="address">VICE memory address to read from.</param>
        /// <param name="length">Count of bytes to read.</param>
        /// <param name="vice">Client for issuing VICE requests.</param>
        /// <param name="addressSpace">Address space to read from.</param>
        /// <returns>Returns a byte array containing bytes read, or null if unable to process the request.</returns>
        public async static Task<byte[]?> ReadViceMemoryAsync(UInt16 address, uint length, IViceMonitorClient vice, AddressSpace addressSpace = AddressSpace.Main)
        {
            // Return an empty array if no bytes were requested.
            if (length == 0)
            {
                return [];
            }

            // Return null if the range is out of bounds.
            var endAddress = (uint)address + length - 1;
            if (endAddress > UInt16.MaxValue)
            {
                return null;
            }


            bool pageSwitchNeeded = false;
            byte oldPage = 0;
            byte newPage = 0;
            ViceMemorySpace viceMemorySpace;

            switch (addressSpace)
            {
                case AddressSpace.Main:
                    viceMemorySpace = ViceMemorySpace.Main;
                    break;

                case AddressSpace.Drive8:
                    viceMemorySpace = ViceMemorySpace.Drive8;
                    break;

                case AddressSpace.Drive9:
                    viceMemorySpace = ViceMemorySpace.Drive9;
                    break;

                case AddressSpace.Drive10:
                    viceMemorySpace = ViceMemorySpace.Drive10;
                    break;

                case AddressSpace.Drive11:
                    viceMemorySpace = ViceMemorySpace.Drive11;
                    break;

                case AddressSpace.COMALPage1:
                    viceMemorySpace = ViceMemorySpace.Main;
                    newPage = 0x80;
                    pageSwitchNeeded = true;
                    break;

                case AddressSpace.COMALPage2:
                    viceMemorySpace = ViceMemorySpace.Main;
                    newPage = 0x81;
                    pageSwitchNeeded = true;
                    break;

                case AddressSpace.COMALPage3:
                    viceMemorySpace = ViceMemorySpace.Main;
                    newPage = 0x82;
                    pageSwitchNeeded = true;
                    break;

                case AddressSpace.COMALPage4:
                    viceMemorySpace = ViceMemorySpace.Main;
                    newPage = 0x83;
                    pageSwitchNeeded = true;
                    break;

                case AddressSpace.COMALPage5:
                    viceMemorySpace = ViceMemorySpace.Main;
                    newPage = 0x84;
                    pageSwitchNeeded = true;
                    break;

                case AddressSpace.COMALPage6:
                    viceMemorySpace = ViceMemorySpace.Main;
                    newPage = 0x85;
                    pageSwitchNeeded = true;
                    break;

                default:
                    return null;
            }

            // Serialize access so that our bank switching doesn't get disrupted.
            await _accessSerializer.WaitAsync();

            byte[]? memory;
            try
            {
                // Switch COMAL banks, if needed. We use the I/O bank to ensure we're accessing the proper memory.
                // TODO: If eventually adding debugging support for real hardware, change how the COMAL page variable is accessed.
                //       Accessing 0xDE00 works in VICE, but doesn't work on real hardware because it is a write-only memory location.
                //       For that, we need to use the shadow variable maintained by COMAL.
                UInt16 io = 3; // TODO: Retrieve bank info during connection to the server, and then use it throughout. (await vice.FindBankAsync("io"))?.Id;
                var restorePage = false;
                try
                {
                    if (pageSwitchNeeded)
                    {
                        oldPage = await vice.ReadByteAsync(((UInt16)(0xDE00)), ViceMemorySpace.Main, bankId: io, sideEffects: false);
                        restorePage = true;
                        await vice.WriteByteAsync((UInt16)(0xDE00), newPage, ViceMemorySpace.Main, bankId: io, sideEffects: true);
                    }

                    //  Read the memory block.
                    memory = await vice.ReadMemoryAsync(address, (UInt16)endAddress, memorySpace: viceMemorySpace);
                }
                finally
                {
                    //  Restore the COMAL bank, if needed.
                    if (restorePage)
                    {
                        await vice.WriteByteAsync((UInt16)(0xDE00), oldPage, ViceMemorySpace.Main, bankId: io, sideEffects: true);
                    }
                }
            }
            catch
            {
                memory = null;
            }
            finally
            {
                // Others can now access memory.
                _accessSerializer.Release();
            }

            return memory;
        }

        public static AddressSpace? AddressSpaceFromName(string name)
        {
            switch (name.Trim().ToLower())
            {
                case "m":
                case "main":
                    return AddressSpace.Main;

                case "d8":
                    return AddressSpace.Drive8;

                case "d9":
                    return AddressSpace.Drive9;

                case "d10":
                    return AddressSpace.Drive10;

                case "d11":
                    return AddressSpace.Drive11;

                case "comal1":
                    return AddressSpace.COMALPage1;

                case "comal2":
                    return AddressSpace.COMALPage2;

                case "comal3":
                    return AddressSpace.COMALPage3;

                case "comal4":
                    return AddressSpace.COMALPage4;

                case "comal5":
                    return AddressSpace.COMALPage5;

                case "comal6":
                    return AddressSpace.COMALPage6;

                default:
                    return null;
            }
        }
    }
}
