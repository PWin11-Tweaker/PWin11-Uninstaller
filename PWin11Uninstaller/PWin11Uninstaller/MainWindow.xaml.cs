using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PWin11Uninstaller.WindowPage;
using System;
using WinRT.Interop;

namespace PWin11Uninstaller
{
    public sealed partial class MainWindow : Window
    {
        private readonly DesktopAcrylicBackdrop acrylicBackdrop = new DesktopAcrylicBackdrop();
        private AppWindow _appWindow;

        public MainWindow()
        {
            this.InitializeComponent();

            // Настройка расширения контента под заголовок
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(SimpleTitleBar); // Используем кастомный TitleBar

            _appWindow = GetAppWindowForCurrentWindow();
            _appWindow.Title = "PWin11 Uninstaller"; // Заголовок окна
            _appWindow.SetIcon("Assets/icon.ico");   // Установка иконки

            // Настройка AcrylicBackdrop
            acrylicBackdrop = new DesktopAcrylicBackdrop();
            this.SystemBackdrop = acrylicBackdrop;
            System.Diagnostics.Debug.WriteLine("MainWindow: AcrylicBackdrop установлен.");

            // Инициализация навигации
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(MainPage));
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                switch (item.Tag.ToString())
                {
                    case "MainPage":
                        ContentFrame.Navigate(typeof(MainPage));
                        break;
                    case "SystemAppsPage":
                        ContentFrame.Navigate(typeof(SystemAppsPage));
                        break;
                    case "AboutPage":
                        ContentFrame.Navigate(typeof(AboutPage));
                        break;
                }
            }
        }
    }
}