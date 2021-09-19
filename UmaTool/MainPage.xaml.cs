using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Capture;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using NotificationsExtensions.Toasts;
using Windows.UI.Notifications;
using UmaTool.Common;

namespace UmaTool
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    /// 
    public sealed partial class MainPage : Page
    {
        ScreenShoter screenShoter;

        public MainPage()
        {
            ApplicationView.PreferredLaunchViewSize = new Size{Width = 480, Height = 720};
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            screenShoter = new ScreenShoter();
            screenShoter.Setup(this);

            this.InitializeComponent();
        }

        public void OnLoadGrid(object sender, RoutedEventArgs e)
        {

            if (!GraphicsCaptureSession.IsSupported())
            {
                // Hide the capture UI if screen capture is not supported.
                screenShotButton.IsEnabled = false;
                adjustPositionButton.IsEnabled = false;
            }
            string version = CommonValues.appSettings.version;
            AppVersion.Text = version;


        }

        private async void StartAutoAnalyze(object sender, RoutedEventArgs e)
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                MessageDialog md = new MessageDialog("分析可能");
                await md.ShowAsync();
            }
        }

        private void Capture(object sender, RoutedEventArgs e)
        {

            screenShoter.StartCaptureAsync();
            CommonMethods.ToastSimpleMessage("スクリーンキャプチャを開始します");

        }

        private void Shot(object sender, RoutedEventArgs e)
        {

            screenShoter.StartCaptureAsync();
            CommonMethods.ToastSimpleMessage("スクリーンショットを撮影しました");

        }

        private async void AdjustPosition(object sender, RoutedEventArgs e)
        {
            await screenShoter.PickWindow();
        }
    }
}
