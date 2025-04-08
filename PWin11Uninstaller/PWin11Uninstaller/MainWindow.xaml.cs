using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace PWin11Uninstaller
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            LoadInstalledPrograms();
        }

        private void LoadInstalledPrograms()
        {
            var programs = new List<ProgramInfo>();
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using (var subKey = key.OpenSubKey(subKeyName))
                        {
                            var displayName = subKey?.GetValue("DisplayName")?.ToString();
                            var uninstallString = subKey?.GetValue("UninstallString")?.ToString();

                            if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(uninstallString))
                            {
                                programs.Add(new ProgramInfo { Name = displayName, UninstallString = uninstallString });
                            }
                        }
                    }
                }
            }
            ProgramsList.ItemsSource = programs;
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ProgramInfo program)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {program.UninstallString}",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                }
                catch (Exception ex)
                {
                    // Обработка ошибок
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Не удалось удалить программу: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
            }
        }
    }

    public class ProgramInfo
    {
        public string? Name { get; set; } // Сделали nullable
        public string? UninstallString { get; set; } // Сделали nullable
    }
}