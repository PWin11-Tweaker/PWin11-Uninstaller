using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using PWin11Uninstaller.WindowPage;
using Microsoft.UI;
using System;
using WinRT.Interop;


namespace PWin11Uninstaller
{
    public sealed partial class MainWindow : Window
    {

        private MicaBackdrop micaBackdrop;
        private AppWindow _appWindow;
        public MainWindow()
        {
            this.InitializeComponent();
            _appWindow = GetAppWindowForCurrentWindow();
            _appWindow.Title = "PWin11";
            _appWindow.SetIcon("Assets/icon.ico");
            micaBackdrop = new MicaBackdrop();
            this.SystemBackdrop = micaBackdrop;
            System.Diagnostics.Debug.WriteLine("MainWindow: MicaBackdrop установлен.");
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