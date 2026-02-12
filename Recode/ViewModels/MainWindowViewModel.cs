using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recode.Core;

namespace Recode.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    int _qualityValue = 50;

    [ObservableProperty]
    Codec _selectedCodec = Codec.H264;

    [ObservableProperty]
    AfterCompletionAction _afterCompletionAction = AfterCompletionAction.Nothing;

    public double OverallProgress => QueueItems.Count == 0 ? 0 : QueueItems.Average(item => item.Progress);

    public ObservableCollection<QueueItemViewModel> QueueItems { get; } = [];

    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (string path in filePaths)
        {
            // skip duplicates
            if (QueueItems.Any(item => item.FilePath == path))
                continue;

            QueueItems.Add(new QueueItemViewModel(path));
        }
    }

    [RelayCommand]
    async Task StartCompression()
    {
        // start compression of all items in queue
    }

    [RelayCommand]
    async Task CancelCompression()
    {
        // clear all items from queue
    }

    [RelayCommand]
    async Task OpenSettings()
    {
        // open settings window
    }
}