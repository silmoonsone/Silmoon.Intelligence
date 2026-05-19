using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Silmoon.Intelligence.WinUIClient.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Silmoon.Intelligence.WinUIClient
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        MainWindowViewModel viewModel;
        public MainWindow()
        {
            InitializeComponent();
            ctlMainWindowPage.DataContext = viewModel = new MainWindowViewModel(ctlMainWindowPage);
            //ctlMainPageFrame.Navigate(typeof(MainPage));
            nameNavigationView.SelectedItem = nameNavigationView.MenuItems[0];
            AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1440, 900));
        }
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = args.SelectedItem as NavigationViewItem;
            var tag = selectedItem.Tag as string;
            switch (tag)
            {
                case "home":
                    nameMainPageFrame.Navigate(typeof(MainPage));
                    break;
                case "chat":
                    nameMainPageFrame.Navigate(typeof(ChatPage));
                    break;
                default:
                    break;
            }
        }

        private void nameBackButton_Click(object sender, RoutedEventArgs e)
        {
            nameMainPageFrame.GoBack();
        }
    }
    public partial class MainWindowViewModel : ObservableObject
    {
        Page Page;
        public MainWindowViewModel(Page page)
        {
            Page = page;
        }
        [RelayCommand]
        void OnNavigateMenuClick()
        {
        }
    }
}
