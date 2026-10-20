using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VICEaRoni.ViewModels
{
    public partial class ConsoleInputViewModel : ViewModelBase
    {
        [ObservableProperty]
        public partial ulong CommandCount { get; set; }

        [ObservableProperty]
        public partial string CommandBuffer { get; set; }

        public Func<string, Task> ProcessCommandAsync { get; set; } = _ => Task.CompletedTask;

        public Func<string, Task> SetCommandAsync { get; set; } = _ => Task.CompletedTask;

        public string CommandText { get; set; } = null!;

        List<string> Commands { get; set; } = new List<string>();

        int _lastRecallIndex = 0;

        public async void HandleEnterKeyAsync()
        {
            var command = CommandBuffer;
            if (command != Commands.LastOrDefault())
            {
                Commands.Add(command);
            }
            await ProcessCommandAsync(command);
        }

        public async void SelectNextCommandAsync()
        {
        }

        public async void SelectPreviousCommandAsync()
        {
            var command = Commands.LastOrDefault() ?? string.Empty;
            await SetCommandAsync(command);
        }
    }
}
