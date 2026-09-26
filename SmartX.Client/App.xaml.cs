// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Client/App.xaml.cs
// Purpose: Application bootstrap with global (dispatcher) exception logging.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Application.DispatcherUnhandledException Event. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception [Accessed 10 September 2026].
//   - Microsoft, 2024. Windows Presentation Foundation (WPF) documentation. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/ [Accessed 10 September 2026].
// ------------------------------------------------------------------------------------
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SmartX.Client;

/// <summary>
/// Application entry point. Installs global exception handlers so an unexpected error
/// surfaces a friendly message (and a crash log) instead of silently closing the window.
/// </summary>
public partial class App : Application
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "smartx-client.log");

    public App()
    {
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log(e.ExceptionObject as Exception, "AppDomain");
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception, "Dispatcher");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails written to:\n{LogPath}",
            "Smart-X Client", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // keep the app alive
    }

    private static void Log(Exception? ex, string source)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTimeOffset.Now:u}] ({source}) {ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }
}
