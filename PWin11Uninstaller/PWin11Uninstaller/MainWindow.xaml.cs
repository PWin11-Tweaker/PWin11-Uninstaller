using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PWin11Uninstaller
{
    public sealed partial class MainWindow : Window
    {
        private ProgramInfo _lastUninstalledProgram;
        private List<ProgramInfo> _allPrograms;

        public MainWindow()
        {
            this.InitializeComponent();
            LoadInstalledPrograms();
        }

        private void LoadInstalledPrograms()
        {
            _allPrograms = new List<ProgramInfo>();
            var programSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Для исключения дубликатов

            // Список веток реестра для проверки
            var registryPaths = new[]
            {
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall", // Для всех пользователей (64-бит)
                @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", // Для 32-битных программ на 64-битной системе
            };

            // Проверяем ветки в HKEY_LOCAL_MACHINE
            foreach (var path in registryPaths)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                var displayName = subKey?.GetValue("DisplayName")?.ToString();
                                var uninstallString = subKey?.GetValue("UninstallString")?.ToString();
                                var version = subKey?.GetValue("DisplayVersion")?.ToString();
                                var publisher = subKey?.GetValue("Publisher")?.ToString();
                                var iconPath = subKey?.GetValue("DisplayIcon")?.ToString();

                                if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(uninstallString) && programSet.Add(displayName))
                                {
                                    _allPrograms.Add(new ProgramInfo
                                    {
                                        Name = displayName,
                                        UninstallString = uninstallString,
                                        Version = version,
                                        Publisher = publisher,
                                        IconPath = iconPath
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // Проверяем ветку в HKEY_CURRENT_USER
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using (var subKey = key.OpenSubKey(subKeyName))
                        {
                            var displayName = subKey?.GetValue("DisplayName")?.ToString();
                            var uninstallString = subKey?.GetValue("UninstallString")?.ToString();
                            var version = subKey?.GetValue("DisplayVersion")?.ToString();
                            var publisher = subKey?.GetValue("Publisher")?.ToString();
                            var iconPath = subKey?.GetValue("DisplayIcon")?.ToString();

                            if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(uninstallString) && programSet.Add(displayName))
                            {
                                _allPrograms.Add(new ProgramInfo
                                {
                                    Name = displayName,
                                    UninstallString = uninstallString,
                                    Version = version,
                                    Publisher = publisher,
                                    IconPath = iconPath
                                });
                            }
                        }
                    }
                }
            }

            _allPrograms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            ProgramsList.ItemsSource = _allPrograms;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allPrograms == null) return;

            string searchText = SearchBox.Text?.Trim().ToLower() ?? string.Empty;
            if (string.IsNullOrEmpty(searchText))
            {
                ProgramsList.ItemsSource = _allPrograms;
            }
            else
            {
                var filteredPrograms = _allPrograms
                    .Where(p => p.Name?.ToLower().Contains(searchText) == true)
                    .ToList();
                ProgramsList.ItemsSource = filteredPrograms;
            }
        }

        private async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ProgramInfo program)
            {
                try
                {
                    _lastUninstalledProgram = program;
                    ScanResidualsButton.IsEnabled = false;
                    DeleteResidualsButton.IsEnabled = false;
                    ResidualsList.Visibility = Visibility.Collapsed;

                    ProgressOverlay.Visibility = Visibility.Visible;
                    ProgressRing.IsActive = true;
                    ProgressText.Text = $"Удаление {program.Name}...";

                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {program.UninstallString}",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });

                    await Task.Run(() => process.WaitForExit());

                    _allPrograms.Remove(program);
                    SearchBox_TextChanged(null, null);

                    ScanResidualsButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Не удалось удалить программу: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                finally
                {
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                    ProgressRing.IsActive = false;
                }
            }
        }

        private async void UninstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedPrograms = ProgramsList.SelectedItems.Cast<ProgramInfo>().ToList();
            if (!selectedPrograms.Any()) return;

            try
            {
                ProgressOverlay.Visibility = Visibility.Visible;
                ProgressRing.IsActive = true;

                foreach (var program in selectedPrograms)
                {
                    ProgressText.Text = $"Удаление {program.Name}...";

                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {program.UninstallString}",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });

                    await Task.Run(() => process.WaitForExit());

                    _lastUninstalledProgram = program;
                    _allPrograms.Remove(program);
                }

                SearchBox_TextChanged(null, null);
                ScanResidualsButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Не удалось удалить программы: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ProgressRing.IsActive = false;
            }
        }

        private async void ScanResiduals_Click(object sender, RoutedEventArgs e)
        {
            if (_lastUninstalledProgram == null) return;

            try
            {
                ProgressOverlay.Visibility = Visibility.Visible;
                ProgressRing.IsActive = true;
                ProgressText.Text = $"Сканирование остатков для {_lastUninstalledProgram.Name}...";

                var residuals = await Task.Run(() => ScanResiduals(_lastUninstalledProgram.Name));
                ResidualsList.ItemsSource = residuals;
                ResidualsList.Visibility = residuals.Any() ? Visibility.Visible : Visibility.Collapsed;
                DeleteResidualsButton.IsEnabled = residuals.Any();
            }
            finally
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ProgressRing.IsActive = false;
            }
        }

        private List<ResidualItem> ScanResiduals(string programName)
        {
            var residuals = new List<ResidualItem>();

            string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            residuals.AddRange(ScanDirectory(programFilesPath, programName));

            string programFilesX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            residuals.AddRange(ScanDirectory(programFilesX86Path, programName));

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            residuals.AddRange(ScanDirectory(appDataPath, programName));

            residuals.AddRange(ScanRegistry(programName));

            return residuals;
        }

        private List<ResidualItem> ScanDirectory(string path, string programName)
        {
            var residuals = new List<ResidualItem>();
            try
            {
                if (Directory.Exists(path))
                {
                    foreach (var dir in Directory.GetDirectories(path, $"*{programName}*", SearchOption.AllDirectories))
                    {
                        residuals.Add(new ResidualItem { Path = dir, IsRegistry = false });
                    }
                    foreach (var file in Directory.GetFiles(path, $"*{programName}*", SearchOption.AllDirectories))
                    {
                        residuals.Add(new ResidualItem { Path = file, IsRegistry = false });
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки доступа
            }
            return residuals;
        }

        private List<ResidualItem> ScanRegistry(string programName)
        {
            var residuals = new List<ResidualItem>();
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"Software"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.Contains(programName, StringComparison.OrdinalIgnoreCase))
                            {
                                residuals.Add(new ResidualItem { Path = $@"HKEY_LOCAL_MACHINE\Software\{subKeyName}", IsRegistry = true });
                            }
                        }
                    }
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.Contains(programName, StringComparison.OrdinalIgnoreCase))
                            {
                                residuals.Add(new ResidualItem { Path = $@"HKEY_CURRENT_USER\Software\{subKeyName}", IsRegistry = true });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки доступа
            }
            return residuals;
        }

        private async void DeleteResiduals_Click(object sender, RoutedEventArgs e)
        {
            var residuals = ResidualsList.ItemsSource as List<ResidualItem>;
            if (residuals == null || !residuals.Any()) return;

            try
            {
                ProgressOverlay.Visibility = Visibility.Visible;
                ProgressRing.IsActive = true;
                ProgressText.Text = "Удаление остатков...";

                foreach (var item in residuals.ToList())
                {
                    if (item.IsRegistry)
                    {
                        await Task.Run(() => DeleteRegistryKey(item.Path));
                    }
                    else
                    {
                        await Task.Run(() => DeleteFileOrDirectory(item.Path));
                    }
                    residuals.Remove(item);
                }

                ResidualsList.ItemsSource = residuals;
                ResidualsList.Visibility = residuals.Any() ? Visibility.Visible : Visibility.Collapsed;
                DeleteResidualsButton.IsEnabled = residuals.Any();
            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Не удалось удалить остатки: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ProgressRing.IsActive = false;
            }
        }

        private void DeleteFileOrDirectory(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки доступа
            }
        }

        private void DeleteRegistryKey(string path)
        {
            try
            {
                if (path.StartsWith(@"HKEY_LOCAL_MACHINE"))
                {
                    var keyPath = path.Replace(@"HKEY_LOCAL_MACHINE\", "");
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true))
                    {
                        if (key != null)
                        {
                            Registry.LocalMachine.DeleteSubKeyTree(keyPath);
                        }
                    }
                }
                else if (path.StartsWith(@"HKEY_CURRENT_USER"))
                {
                    var keyPath = path.Replace(@"HKEY_CURRENT_USER\", "");
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                    {
                        if (key != null)
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(keyPath);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки доступа
            }
        }
    }

    public class ProgramInfo
    {
        public string? Name { get; set; }
        public string? UninstallString { get; set; }
        public string? Version { get; set; }
        public string? Publisher { get; set; }
        public string? IconPath { get; set; }
    }

    public class ResidualItem
    {
        public string Path { get; set; }
        public bool IsRegistry { get; set; }
    }
}