using System.IO;
using System.Windows;

namespace Pastille;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        Localization.Initialize();
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(
                System.Globalization.CultureInfo.CurrentUICulture.IetfLanguageTag)));
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Report(args.Exception);
            args.Handled = true;
        };
        if (e.Args.Contains("--self-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                await SelfTests.RunAsync();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-error.txt"), ex.ToString());
                Shutdown(1);
            }
            return;
        }
        var window = new MainWindow();
        MainWindow = window;
        try { await window.StartInTrayAsync(); }
        catch (Exception ex)
        {
            Report(ex);
            window.RequestExit();
        }
    }

    internal static void Log(Exception ex)
    {
        Directory.CreateDirectory(Settings.DataDirectory);
        File.AppendAllText(Path.Combine(Settings.DataDirectory, "errors.log"),
            $"{DateTimeOffset.Now:O} {ex}{Environment.NewLine}");
    }

    internal static void Report(Exception ex)
    {
        Log(ex);
        MessageBox.Show(ex.Message, Localization.Text("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
