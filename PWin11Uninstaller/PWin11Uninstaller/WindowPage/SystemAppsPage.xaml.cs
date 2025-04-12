using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Diagnostics;
using System.Security.Principal;

namespace PWin11Uninstaller.WindowPage
{
    public sealed partial class SystemAppsPage : Page
    {
        private List<SystemAppInfo> _systemApps = new List<SystemAppInfo>();
        private SystemAppInfo? _edgeApp; // Для хранения информации о Microsoft Edge

        public SystemAppsPage()
        {
            this.InitializeComponent();
            LoadSystemApps();
        }

        private bool IsRunningAsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private async void LoadSystemApps()
        {
            _systemApps = new List<SystemAppInfo>();
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser("").ToList();

            ProgressOverlay.Visibility = Visibility.Visible;
            ProgressRing.IsActive = true;
            ProgressText.Text = "Сканирование приложений...";

            var fadeInStoryboard = (Storyboard)Resources["FadeInOverlay"];
            fadeInStoryboard.Begin();

            ProgressBar.Maximum = packages.Count;
            ProgressBar.Value = 0;

            foreach (var package in packages)
            {
                try
                {
                    var appInfo = new SystemAppInfo
                    {
                        Name = package.DisplayName,
                        PackageFullName = package.Id.FullName,
                        Publisher = package.PublisherDisplayName
                    };

                    if (string.IsNullOrEmpty(appInfo.Name))
                        continue;

                    // Проверяем, является ли приложение Microsoft Edge
                    if (appInfo.Name.Contains("Microsoft Edge", StringComparison.OrdinalIgnoreCase))
                    {
                        _edgeApp = appInfo;
                        RemoveEdgeButton.IsEnabled = true; // Активируем кнопку
                    }
                    else
                    {
                        var logoUri = package.Logo;
                        if (logoUri != null)
                        {
                            appInfo.IconSource = await LoadImageFromUri(logoUri);
                        }

                        _systemApps.Add(appInfo);
                    }

                    ProgressBar.Value += 1;
                    ProgressText.Text = $"Обработано {_systemApps.Count + (_edgeApp != null ? 1 : 0)} из {packages.Count} приложений...";

                    await Task.Delay(1);
                }
                catch
                {
                    // Игнорируем ошибки для отдельных пакетов
                }
            }

            _systemApps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            SystemAppsList.ItemsSource = _systemApps;

            var fadeOutStoryboard = (Storyboard)Resources["FadeOutOverlay"];
            fadeOutStoryboard.Completed += (s, e) =>
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
            };
            fadeOutStoryboard.Begin();
            ProgressRing.IsActive = false;
        }

        private async Task<BitmapImage?> LoadImageFromUri(Uri uri)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(uri);
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                        {
                            writer.WriteBytes(imageBytes);
                            await writer.StoreAsync();
                        }

                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(stream);
                        return bitmapImage;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private async void RemoveEdge_Click(object sender, RoutedEventArgs e)
        {
            if (_edgeApp == null) return;

            if (!IsRunningAsAdministrator())
            {
                await new ContentDialog
                {
                    Title = "Ошибка",
                    Content = "Для удаления Microsoft Edge требуются права администратора.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                return;
            }

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Подтверждение удаления",
                Content = "Вы уверены, что хотите удалить Microsoft Edge? Это может повлиять на работу системы.",
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
                ProgressText.Text = "Удаление Microsoft Edge...";
                var fadeInStoryboard = (Storyboard)Resources["FadeInOverlay"];
                fadeInStoryboard.Begin();

                // Запускаем PowerShell для удаления Edge
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-AppxPackage *MicrosoftEdge* | Remove-AppxPackage\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = new Process { StartInfo = processInfo })
                {
                    process.Start();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode != 0)
                    {
                        string error = await process.StandardError.ReadToEndAsync();
                        throw new Exception($"Ошибка PowerShell: {error}");
                    }
                }

                // Удаляем Edge из списка (если он там был)
                if (_edgeApp != null)
                {
                    _systemApps.Remove(_edgeApp);
                    SystemAppsList.ItemsSource = _systemApps.ToList();
                    _edgeApp = null;
                    RemoveEdgeButton.IsEnabled = false; // Деактивируем кнопку
                }
            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Не удалось удалить Microsoft Edge: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                var fadeOutStoryboard = (Storyboard)Resources["FadeOutOverlay"];
                fadeOutStoryboard.Completed += (s, e) =>
                {
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                };
                fadeOutStoryboard.Begin();
                ProgressRing.IsActive = false;
            }
        }

        private async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SystemAppInfo app)
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Подтверждение удаления",
                    Content = $"Вы уверены, что хотите удалить {app.Name}?",
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
                    ProgressText.Text = $"Удаление {app.Name}...";
                    var fadeInStoryboard = (Storyboard)Resources["FadeInOverlay"];
                    fadeInStoryboard.Begin();

                    var packageManager = new PackageManager();
                    var operation = packageManager.RemovePackageAsync(app.PackageFullName, RemovalOptions.None);
                    await operation.AsTask();

                    _systemApps.Remove(app);
                    SystemAppsList.ItemsSource = _systemApps.ToList();
                }
                catch (Exception ex)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Не удалось удалить приложение: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                finally
                {
                    var fadeOutStoryboard = (Storyboard)Resources["FadeOutOverlay"];
                    fadeOutStoryboard.Completed += (s, e) =>
                    {
                        ProgressOverlay.Visibility = Visibility.Collapsed;
                    };
                    fadeOutStoryboard.Begin();
                    ProgressRing.IsActive = false;
                }
            }
        }

        private async void UninstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = SystemAppsList.SelectedItems.Cast<SystemAppInfo>().ToList();
            if (!selectedApps.Any()) return;

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Подтверждение удаления",
                Content = $"Вы уверены, что хотите удалить {selectedApps.Count} приложений?",
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
                var fadeInStoryboard = (Storyboard)Resources["FadeInOverlay"];
                fadeInStoryboard.Begin();

                var packageManager = new PackageManager();
                foreach (var app in selectedApps)
                {
                    ProgressText.Text = $"Удаление {app.Name}...";
                    var operation = packageManager.RemovePackageAsync(app.PackageFullName, RemovalOptions.None);
                    await operation.AsTask();
                    _systemApps.Remove(app);
                }

                SystemAppsList.ItemsSource = _systemApps.ToList();
            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Не удалось удалить приложения: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                var fadeOutStoryboard = (Storyboard)Resources["FadeOutOverlay"];
                fadeOutStoryboard.Completed += (s, e) =>
                {
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                };
                fadeOutStoryboard.Begin();
                ProgressRing.IsActive = false;
            }
        }
    }

    public class SystemAppInfo
    {
        public string? Name { get; set; }
        public string? PackageFullName { get; set; }
        public string? Publisher { get; set; }
        public BitmapImage? IconSource { get; set; }
    }
}