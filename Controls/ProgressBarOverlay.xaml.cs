using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace PdfFormFramework.Controls;

public partial class ProgressBarOverlay : ContentView
{
    public ProgressBarOverlay()
    {
        InitializeComponent();
    }
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                loadinggif.IsAnimationPlaying = true;
            });
        }
    }
    public void Show()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            progressBarOverlay.IsVisible = true;
        });
    }
    public async Task ShowAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            progressBarOverlay.IsVisible = true;
        });
    }

    public async Task Hide()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            progressBarOverlay.IsVisible = false;
            //progressBarOverlay.InvalidateMeasure();
        });
    }
}
