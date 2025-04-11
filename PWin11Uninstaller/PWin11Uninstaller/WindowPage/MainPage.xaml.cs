using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Drawing.Imaging;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PWin11Uninstaller.WindowPage
{
    public sealed partial class MainPage : Page
    {
        private ProgramInfo? _lastUninstalledProgram;
        private List<ProgramInfo> _allPrograms = new List<ProgramInfo>();

        public MainPage()
        {
            this.InitializeComponent();
            LoadInstalledPrograms();
        }

        private async void LoadInstalledPrograms()
        {
            _allPrograms = new List<ProgramInfo>();
            var programSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var registryPaths = new[]
            {
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            };

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
                                    var program = new ProgramInfo
                                    {
                                        Name = displayName,
                                        UninstallString = uninstallString,
                                        Version = version,
                                        Publisher = publisher,
                                        IconPath = iconPath
                                    };
                                    program.IconSource = await ExtractIcon(iconPath);
                                    _allPrograms.Add(program);
                                }
                            }
                        }
                    }
                }
            }

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
                                var program = new ProgramInfo
                                {
                                    Name = displayName,
                                    UninstallString = uninstallString,
                                    Version = version,
                                    Publisher = publisher,
                                    IconPath = iconPath
                                };
                                program.IconSource = await ExtractIcon(iconPath);
                                _allPrograms.Add(program);
                            }
                        }
                    }
                }
            }

            _allPrograms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            ProgramsList.ItemsSource = _allPrograms;
        }

        private async Task<BitmapImage?> ExtractIcon(string? iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return null;

            try
            {
                string path = iconPath;
                int iconIndex = 0;

                if (iconPath.Contains(","))
                {
                    var parts = iconPath.Split(',');
                    path = parts[0].Trim();
                    iconIndex = int.Parse(parts[1].Trim());
                }

                if (!File.Exists(path))
                    return null;

                using (var icon = Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null)
                        return null;

                    using (var bitmap = icon.ToBitmap())
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            bitmap.Save(memoryStream, ImageFormat.Png);
                            memoryStream.Position = 0;

                            var bitmapImage = new BitmapImage();
                            using (var randomAccessStream = new InMemoryRandomAccessStream())
                            {
                                using (var writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
                                {
                                    writer.WriteBytes(memoryStream.ToArray());
                                    await writer.StoreAsync();
                                }

                                await bitmapImage.SetSourceAsync(randomAccessStream);
                            }
                            return bitmapImage;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs? e)
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
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Подтверждение удаления",
                    Content = $"Вы уверены, что хотите удалить {program.Name}?",
                    PrimaryButtonText = "Да",
                    CloseButtonText = "Нет",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                    return;

                try
                {
                    _lastUninstalledProgram = program;
                    ScanResidualsButton.IsEnabled = false;
                    DeleteResidualsButton.IsEnabled = false;
                    ResidualsList.Visibility = Visibility.Collapsed;

                    ProgressOverlay.Visibility = Visibility.Visible;
                    ProgressRing.IsActive = true;
                    ProgressText.Text = $"Удаление {program.Name}...";

                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {program.UninstallString}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };

                    using (var process = new System.Diagnostics.Process { StartInfo = processInfo })
                    {
                        process.Start();
                        await Task.Run(() => process.WaitForExit());
                    }

                    _allPrograms.Remove(program);
                    // Вместо SearchBox_TextChanged(sender, null) вызываем обновление списка напрямую
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

                    ScanResidualsButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Не удалось удалить программу: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
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

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Подтверждение удаления",
                Content = $"Вы уверены, что хотите удалить {selectedPrograms.Count} программ?",
                PrimaryButtonText = "Да",
                CloseButtonText = "Нет",
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            try
            {
                ProgressOverlay.Visibility = Visibility.Visible;
                ProgressRing.IsActive = true;

                foreach (var program in selectedPrograms)
                {
                    ProgressText.Text = $"Удаление {program.Name}...";

                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {program.UninstallString}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };

                    using (var process = new System.Diagnostics.Process { StartInfo = processInfo })
                    {
                        process.Start();
                        await Task.Run(() => process.WaitForExit());
                    }

                    _lastUninstalledProgram = program;
                    _allPrograms.Remove(program);
                }

                // Обновляем список программ
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

                ScanResidualsButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Не удалось удалить программы: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
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
            if (_lastUninstalledProgram is null) return;

            try
            {
                ProgressOverlay.Visibility = Visibility.Visible;
                ProgressRing.IsActive = true;
                ProgressText.Text = $"Сканирование остатков для {_lastUninstalledProgram.Name ?? "неизвестной программы"}...";

                var residuals = await Task.Run(() => ScanResiduals(_lastUninstalledProgram.Name ?? string.Empty));
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

            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            residuals.AddRange(ScanDirectory(programDataPath, programName));

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
                    if (item.Path != null) // Проверка на null
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
                    XamlRoot = this.XamlRoot
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
        public BitmapImage? IconSource { get; set; }
    }

    public class ResidualItem
    {
        public string? Path { get; set; }
        public bool IsRegistry { get; set; }
    }
}