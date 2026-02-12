using CommunityToolkit.Mvvm.ComponentModel;

namespace Recode.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    int _qualityValue = 50;
}