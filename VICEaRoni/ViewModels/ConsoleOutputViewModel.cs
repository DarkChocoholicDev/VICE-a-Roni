using Avalonia.Controls;
using Avalonia.Controls.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VICEaRoni.ViewModels
{
    public partial class ConsoleOutputViewModel : ViewModelBase
    {
        [ObservableProperty]
        public partial ulong LineCount { get; set; }

        /// <summary>
        /// Count of newline-terminated lines in the buffer.
        /// </summary>
        public InlineCollection Inlines { get; set; } = [];
        public ScrollViewer? ScrollViewer { get; set; }

        public void Write(char c)
        {
            Write($"{c}");
        }

        public void Write(string text)
        {
            Inlines.Add(new Run(text));
            ScrollToEnd();
            LineCount += (uint)text.Count(c => c == '\n');
        }

        public void WriteLine()
        {
            WriteLine(string.Empty);
        }

        public void WriteLine(string text)
        {
            Inlines.Add(new Run(text + '\n'));
            ScrollToEnd();
            LineCount += (uint)text.Count(c => c == '\n') + 1;
        }

        public void ScrollToEnd()
        {
            if (ScrollViewer != null)
            {
                ScrollViewer.ScrollToEnd();
            }
        }
    }
}
