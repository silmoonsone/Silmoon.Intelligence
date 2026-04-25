using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Silmoon.Extensions.Hosting.Extensions;
using Silmoon.Extensions.Hosting.Interfaces;
using Silmoon.Intelligence.WinUIClient.Pages;
using Silmoon.Intelligence.Hosting.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Silmoon.Intelligence.WinUIClient
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public MainWindow MainWindow { get; set; }
        static IHost Host { get; set; }
        static IServiceProvider ServiceProvider => Host.Services;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

            builder.Services.AddSingleton<ISilmoonConfigureService, SilmoonConfigureServiceImpl>();
            builder.Services.AddSingleton<ClientService>();
            builder.Services.AddSingleton<ToolFunctionService>();
            builder.Services.AddHostedService(provider => provider.GetRequiredService<ClientService>());

            builder.Services.AddSilmoonConfigure<SilmoonConfigureServiceImpl>(o =>
            {
#if DEBUG
                o.DebugConfig();
#else
                o.ReleaseConfig();
#endif
            });
            Host = builder.Build();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            await Host.StartAsync();

            MainWindow = new MainWindow();
            MainWindow.ExtendsContentIntoTitleBar = true;
            MainWindow.ctlMainPageFrame.Navigate(typeof(MainPage));
            MainWindow.Activate();


        }
        public static T GetService<T>() => ServiceProvider.GetService<T>();
    }
}
