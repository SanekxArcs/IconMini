using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconMini;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            Environment.ExitCode = ConversionChecks.Run();
            return;
        }
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parameters =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new IconApp();
        });
    }
}

public sealed partial class IconApp : Application
{
    private Window? window;
    public IconApp()
    {
        RequestedTheme = ApplicationTheme.Dark;
        UnhandledException += (_, e) =>
        {
            LogError(e.Exception);
        };
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            window = new MainWindow();
            window.Activate();
            if (Environment.GetCommandLineArgs().Contains("--smoke-test"))
                ((MainWindow)window).StartSmokeTest();
        }
        catch (Exception ex)
        {
            LogError(ex);
            throw;
        }
    }

    private static void LogError(Exception ex)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IconMini");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "last-error.txt"), ex.ToString());
        }
        catch { /* Diagnostics must never prevent startup. */ }
    }
}
