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

namespace PWin11Uninstaller.WindowPage
{
    public sealed partial class SystemAppsPage : Page
    {
        private List<SystemAppInfo> _systemApps = new List<SystemAppInfo>();

        public SystemAppsPage()
        {
            this.InitializeComponent();
            LoadSystemApps();
        }

        private async void LoadSystemApps()
        {
            _systemApps = new List<SystemAppInfo>();
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser("").ToList(); // Преобразуем в список для подсчёта

            // Показываем прогресс-бар и анимацию
            ProgressOverlay.Visibility = Visibility.Visible;
            ProgressRing.IsActive = true;
            ProgressText.Text = "Сканирование приложений...";

            // Запускаем анимацию затемнения
            var fadeInStoryboard = (Storyboard)Resources["FadeInOverlay"];
            fadeInStoryboard.Begin();

            // Настраиваем ProgressBar
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

                    var logoUri = package.Logo;
                    if (logoUri != null)
                    {
                        appInfo.IconSource = await LoadImageFromUri(logoUri);
                    }

                    _systemApps.Add(appInfo);

                    // Обновляем прогресс
                    ProgressBar.Value += 1;
                    ProgressText.Text = $"Обработано {_systemApps.Count} из {packages.Count} приложений...";

                    // Даём UI обновиться
                    await Task.Delay(1);
                }
                catch
                {
                    // Игнорируем ошибки для отдельных пакетов
                }
            }

            _systemApps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            SystemAppsList.ItemsSource = _systemApps;

            // Скрываем прогресс-бар с анимацией
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