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
using Windows.ApplicationModel;
using System.Threading;

namespace UmaTool
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ScreenShoter screenShoter;
        private const int maxConsoleCharsCount = 4096;
        private EventData[] eventDataList;
        string[] ocrTexts;
        int preEventIndex = -1;

        bool isAnalyerActive = false;
        CancellationTokenSource analyzerTokenSource;

        public MainPage()
        {
            //ApplicationView.PreferredLaunchViewSize = new Windows.Foundation.Size{Width = 480, Height = 720};
            //ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            Application.Current.Suspending += new SuspendingEventHandler(this.App_Suspending);
            Application.Current.Resuming += new EventHandler<object>(this.App_Resuming);

            screenShoter = new ScreenShoter();
            eventDataList = EventData.GetEventDataList();
            isAnalyerActive = false;

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

            // バージョンの記載
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            AppVersion.Text = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

            var ryaku = previewPanel.TransformToVisual(mainGrid).TransformPoint(new Windows.Foundation.Point(0, 0));

            // スクリーンショットを投影する位置を調整
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
            if (isAnalyerActive)
            {
                // 解析アクティブ状態
                this.analyzerTokenSource.Cancel();

                this.selectWindowButton.IsEnabled = true;
                this.isAnalyerActive = false;
                this.resultGrid.Visibility = Visibility.Collapsed;

            }
            else
            {
                // 解析非アクティブ状態

                this.selectWindowButton.IsEnabled = false;
                isAnalyerActive = true;
                this.resultGrid.Visibility = Visibility.Visible;

                if (!screenShoter.isAvarableScreenShot())
                {
                    CommonMethods.ToastSimpleMessage("ウィンドウを選択してください", toastType: MessageType.Caution);
                    return;
                }

                this.analyzerTokenSource = new CancellationTokenSource();
                var analyzerCancelToken = analyzerTokenSource.Token;
                CommonMethods.ToastSimpleMessage("解析を開始します");
                Task.Run(
                    () => ConstantAnalyze(1000, analyzerCancelToken),
                    analyzerCancelToken
                    );
            }
        }

        private async void ConstantAnalyze(int sleepSpan, CancellationToken cancelToken) {
            //無限ループする
            await Task.Run(()=> LoopAnalyze(sleepSpan, cancelToken));
        }

        private async void LoopAnalyze(int sleepSpan, CancellationToken cancelToken)
        {
            while (true)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }
                await Analyze();
                await Task.Delay(sleepSpan);
            }
        }

        private async Task Analyze()
        {
            int left;
            int top;
            int width;
            int height;

            int cLeft;
            int cTop;
            int cRight;
            int cBottom;

            WriteConsole("選択肢解析中...", MessageType.Information);
            ocrTexts = new string[GrobalValues.appSettings.ocrRangesDic["twoChoise"].Length];

            // まずは画面キャプチャから
            int i=0;
            foreach (var range in GrobalValues.appSettings.ocrRangesDic["twoChoise"])
            {
                cLeft = GrobalValues.appSettings.clipRangeDic["twoChoise"].left;
                cTop = GrobalValues.appSettings.clipRangeDic["twoChoise"].top;
                cRight = GrobalValues.appSettings.clipRangeDic["twoChoise"].right;
                cBottom = GrobalValues.appSettings.clipRangeDic["twoChoise"].top;

                left = GrobalValues.appSettings.clipRangeDic["twoChoise"].left + (int)(range.left * (screenShoter.frameWidth - cLeft - cTop) / 100);
                top = GrobalValues.appSettings.clipRangeDic["twoChoise"].top + (int)(range.top * screenShoter.frameHeight / 100);
                width = (int)(range.width * screenShoter.frameWidth / 100);
                height = (int)(range.height * screenShoter.frameHeight / 100);

                ocrTexts[i] = (await this.ImageRangeToString(left, top, width, height));
                WriteConsole($"選択肢{i+1}:{ocrTexts[i]}", MessageType.Information);

                i++;
            }

            int minDistance = 0;
            foreach(var str in ocrTexts)
            {
                if(str != null)minDistance += str.Length;
            }

            minDistance = (int)(minDistance * GrobalValues.appSettings.defDistRate);

            if (minDistance < GrobalValues.appSettings.minEventStrLength)
            {
                // 閾値より少ない文字列には検索をかけない
                updateChoice(
                    "",
                    "",
                    "",
                    "",
                    ""
                    );
                return;
            }

            int minDistanceIndex = -1;
            int distance = 0;

            i = 0;
            foreach (EventData eventData in eventDataList) { 
                // 登録されているイベントを総ループ
                if(eventData.choices != null && eventData.choices.Length == ocrTexts.Length)
                {
                    distance = 0;
                    //イベント選択肢が2つのもののみ
                    for (int j = 0;j < 2; j++)
                    {
                        //レーベンシュテイン距離を測定し、二つの選択肢の合計距離を算出する
                        distance += Fastenshtein.Levenshtein.Distance(eventData.choices[j].text,ocrTexts[j]);
                    }
                    
                    if(distance < minDistance)
                    {
                        // 最小距離の更新
                        minDistance = distance;
                        minDistanceIndex = i;
                    }
                }
                i++;
            }



            if (minDistanceIndex == this.preEventIndex) {
                //結果がさっきと同じなら終了
                return;
                
            } else if (minDistanceIndex == -1) {
                // 結果が初期から変化しない場合は終了
                updateChoice(
                    "",
                    "",
                    "",
                    "",
                    ""
                    );
                return;
            }

            WriteConsole($"解析結果:{eventDataList[minDistanceIndex].title}",MessageType.Information);
            updateChoice(
                eventDataList[minDistanceIndex].title,
                eventDataList[minDistanceIndex].choices[0].text,
                eventDataList[minDistanceIndex].choices[0].effect,
                eventDataList[minDistanceIndex].choices[1].text,
                eventDataList[minDistanceIndex].choices[1].effect
                );


        }

        private async Task<String> ImageRangeToString(int left = -1, int top = -1, int width = -1, int height = -1)
        {
            var bmp = screenShoter.GetCurrentSoftwareBitMap(left, top, width, height);
            var ocr = await this.BmpToString(bmp);
            return ocr.Text;
        }

        private async Task<OcrResult> BmpToString(SoftwareBitmap image)
        {
            OcrEngine ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            // OCR実行
            var ocrResult = await ocrEngine.RecognizeAsync(image);
            return ocrResult;
        }

        private void Capture(object sender, RoutedEventArgs e)
        {
            if (screenShoter.isAvarableScreenShot()) {
                CommonMethods.ToastSimpleMessage("スクリーンキャプチャを開始します");
            }
            else
            {
                CommonMethods.ToastSimpleMessage("ウィンドウを選択してください",toastType: MessageType.Caution);
            }
        }

        private async void Shot(object sender, RoutedEventArgs e)
        {
            if (!screenShoter.isAvarableScreenShot())
            {
                CommonMethods.ToastSimpleMessage("ウィンドウを選択してください", toastType: MessageType.Caution);
                return;
            }

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

        private void updateChoice(string eventTitle, string choice1, string effect1, string choice2, string effect2) {
            EditTextBlockSync(this.eventTitle, eventTitle);
            EditTextBlockSync(this.choice1, choice1);
            EditTextBlockSync(this.effect1, effect1);
            EditTextBlockSync(this.choice2, choice2);
            EditTextBlockSync(this.effect2, effect2);
        }

        /// <summary>
        /// テキストブロックを非同期的に編集します
        /// </summary>
        /// <param name="control">テキストブロックコントロール</param>
        /// <param name="text">代入する文字列</param>
        private void EditTextBlockSync(TextBlock control , string text) {
            Task.Run(async () =>
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //ユーザーインターフェースを操作する
                    control.Text = text;
                });
            });
        }


        private void WriteConsole(string message, MessageType type = MessageType.Information) {
            // ついでにログにも書き込んじゃう
            try
            {
                EditTextBlockSync(this.consoleBox, CommonMethods.SubstringSafe(message + "\n" + consoleBox.Text, 0, 4096));
                CommonMethods.appendLog(message, type);
            }
            catch { }
        }

    }
}
