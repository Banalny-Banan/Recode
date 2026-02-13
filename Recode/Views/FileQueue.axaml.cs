using Avalonia.Controls;
using Recode.ViewModels;

namespace Recode.Views;

public partial class FileQueue : UserControl
{
    public FileQueue()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            DataContext = new MainWindowViewModel
            {
                QueueItems =
                {
                    new("video1.mp4", "1.2 GB", 60, "ETA 2:30"),
                    new("video2.mkv", "800 MB", 0, "Pending"),
                    new("video3.avi", "2.5 GB", 100, "Completed"),
                    new("video4.mov", "3 GB", 25, "ETA 5:00"),
                    new("video5.flv", "500 MB", 90, "ETA 0:30"),
                    new("video6.wmv", "1 GB", 10, "ETA 10:00"),
                    new("video7.mp4", "700 MB", 75, "ETA 1:00"),
                    new("video8.mkv", "1.5 GB", 50, "ETA 3:00"),
                },
            };
        }
    }
}