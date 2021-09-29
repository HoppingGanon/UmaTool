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
using Windows.Storage;
using System.Numerics;
using Windows.UI.Composition;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Drawing;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.UI.Core;

namespace UmaTool
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ScreenShoter screenShoter;
        const int maxConsoleCharsCount = 4096;

        public MainPage()
        {
            ApplicationView.PreferredLaunchViewSize = new Windows.Foundation.Size{Width = 480, Height = 720};
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            Application.Current.Suspending += new SuspendingEventHandler(this.App_Suspending);
            Application.Current.Resuming += new EventHandler<object>(this.App_Resuming);

            screenShoter = new ScreenShoter();

            this.InitializeComponent();
        }

        public void OnLoadGrid(object sender, RoutedEventArgs e)
        {

            if (!GraphicsCaptureSession.IsSupported())
            {
                // Hide the capture UI if screen capture is not supported.
                screenShotButton.IsEnabled = false;
                selectWindowButton.IsEnabled = false;
            }
            string version = GrobalValues.appSettings.version;
            AppVersion.Text = version;

            var ryaku = previewPanel.TransformToVisual(mainGrid).TransformPoint(new Windows.Foundation.Point(0, 0));

            var pos = new Vector3((float)ryaku.X, (float)ryaku.Y, 1f);
            var siz = previewPanel.ActualSize;

            screenShoter.Setup(this,
                    pos,
                    siz
                );
            WriteConsole($"position {pos.ToString()}");
            WriteConsole($"size {previewPanel.ActualSize.ToString()}");
            
        }

        private void StartAutoAnalyze(object sender, RoutedEventArgs e)
        {
            CommonMethods.ToastSimpleMessage("解析を開始します");
            Task.Run(
                () => Analyze()
                );

        }

        private async void Analyze()
        {
            var windowName = CommonMethods.RemoveInvalidFileChar(screenShoter.GetWindowName());
            var bmp = screenShoter.GetCurrentSoftwareBitMap(0,0,100,100);

            //保存
            CommonMethods.SaveSoftwareBitmap(bmp, KnownFolders.PicturesLibrary, $"Ocr_{windowName}.png");

            var ocr = await this.GetCurrentFrameOcr(bmp);
            
            this.WriteConsole($"読み取り結果 : {ocr.Text}");
        }

        private async Task<OcrResult> GetCurrentFrameOcr(SoftwareBitmap image)
        {
            OcrEngine ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            // OCR実行
            var ocrResult = await ocrEngine.RecognizeAsync(image);
            return ocrResult;
        }

        private void Capture(object sender, RoutedEventArgs e)
        {
            CommonMethods.ToastSimpleMessage("スクリーンキャプチャを開始します");
        }

        private async void Shot(object sender, RoutedEventArgs e)
        {
            if (screenShoter.isAvarableScreenShot())
            {
                var windowName = CommonMethods.RemoveInvalidFileChar(screenShoter.GetWindowName());
                var resultPath = await screenShoter.Screenshot(KnownFolders.PicturesLibrary, $"{windowName}.png");
                if (resultPath.ex == null)
                {
                    CommonMethods.ToastSimpleMessage("スクリーンショットを撮影しました", resultPath.path, MessageType.Success, isWritingLog: false);
                    WriteConsole($"'{resultPath.path}'にスクリーンショットを保存しました", MessageType.Success);
                }
                else
                {
                    CommonMethods.ToastSimpleMessage("スクリーンショットの撮影に失敗しました",toastType:MessageType.Error);
                    WriteConsole(resultPath.ex.Message, MessageType.Error);
                }
            }
        }

        private async void SelectWindow(object sender, RoutedEventArgs e)
        {
            // ファイル名に使用できない文字の排除
            var windowName = CommonMethods.RemoveInvalidFileChar(await screenShoter.PickWindow());

            screenShoter.StartCaptureAsync();
            WriteConsole("ウィンドウ補足 : " + windowName);

        }

        private void App_Suspending(Object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            CommonMethods.ToastSimpleMessage("終了処理");
            screenShoter.StopCapture();
        }

        private void App_Resuming(object sender, object e)
        {

        }

        private void WriteConsole(string message, MessageType type = MessageType.Information) {
            // ついでにログにも書き込んじゃう
            try
            {
                Task.Run(async () =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //ユーザーインターフェースを操作する
                        this.consoleBox.Text = CommonMethods.SubstringSafe(message + "\n" + consoleBox.Text, 0, 4096);
                    });
                });

                CommonMethods.appendLog(message, type);
            }
            catch { }
        }

    }
}
