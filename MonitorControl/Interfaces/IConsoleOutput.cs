using System;
using System.Collections.Generic;
using System.Text;

namespace MonitorControl
{
    public interface IConsoleOutput
    {
        /// <summary>
        /// Writes a character to the console.
        /// </summary>
        /// <param name="c">Character to write.</param>
        public void Write(char c);

        /// <summary>
        /// Writes a string to the console.
        /// </summary>
        /// <param name="text">String to write.</param>
        public void Write(string text);

        /// <summary>
        /// Writes a newline character to the console.
        /// </summary>
        public void WriteLine();

        /// <summary>
        /// Writes a string to the console, followed by a newline character.
        /// </summary>
        /// <param name="text">String to write.</param>
        public void WriteLine(string text);
    }
}
