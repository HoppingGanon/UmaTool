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
        // スクリーンプレビューをつかさどるobject
        private ScreenShoter screenShoter;
        // 読み込んだイベントデータ
        private EventData[] eventDataList;
        // 読み取ったOCRのデータ
        private string[] ocrTexts;
        // //前回読み込んだイベント番号(2回連続同じなら処理をスキップ)
        private int preEventIndex = -1;
        // 解析がアクティブ状態かどうか
        private bool isAnalyerActive = false;
        // 解析を途中で停止するためのキャンセルトークン
        private CancellationTokenSource analyzerTokenSource;

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

        /// <summary>
        /// メインフレームがロードされる際のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            // イベントデータがなければボタンを非活性化
            if (eventDataList.Length == 0)
            {
                this.autoAnalyzeButton.IsEnabled = false;
            }
        }

        private async void PickEventDataJsonPath(object sender, RoutedEventArgs e) {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(".json");

            // ファイルピッカーでファイルを開く
            StorageFile file = await picker.PickSingleFileAsync();

            if (file == null) {
                return;
            }

            // ファイルをコピーする
            await file.CopyAsync(ApplicationData.Current.LocalFolder, Common.EventData.fileName, NameCollisionOption.ReplaceExisting);

            await Task.Delay(2000);

            // 再度イベントデータの読み込み
            this.eventDataList = Common.EventData.GetEventDataList();
            if(this.eventDataList.Length == 0)
            {
                // 読み込めていない場合
                this.autoAnalyzeButton.IsEnabled = false;
            }
            else
            {
                // 読み込めた場合
                this.autoAnalyzeButton.IsEnabled = true;
            }


        }

        /// <summary>
        /// 選択肢自動解析ボタンのクリックで発火するメソッド
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartAutoAnalyze(object sender, RoutedEventArgs e)
        {
            if (isAnalyerActive)
            {
                // 解析アクティブ状態
                this.analyzerTokenSource.Cancel();

                this.selectWindowButton.IsEnabled = true;
                this.isAnalyerActive = false;
                this.resultGrid.Visibility = Visibility.Collapsed;
                this.autoAnalyzeButton.Content = "選択肢自動解析";

            }
            else if (screenShoter.isAvarableScreenShot())
            {
                // 解析非アクティブ状態かつ画面指定状態

                this.selectWindowButton.IsEnabled = false;
                this.isAnalyerActive = true;
                this.resultGrid.Visibility = Visibility.Visible;
                this.autoAnalyzeButton.Content = "解析停止";

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
            else
            {
                //解析非アクティブ状態でウィンドウ未指定
                CommonMethods.ToastSimpleMessage("ウィンドウを選択してください", toastType: MessageType.Caution);
            }
        }

        /// <summary>
        /// 継続的に解析するタスクを入れ込むメソッド
        /// </summary>
        /// <param name="sleepSpan">1ループ後にタイキする時間(ms)</param>
        /// <param name="cancelToken">キャンセルトークン</param>
        private async void ConstantAnalyze(int sleepSpan, CancellationToken cancelToken) {
            //無限ループする
            await Task.Run(()=> LoopAnalyze(sleepSpan, cancelToken));
        }

        /// <summary>
        /// 継続的に解析する
        /// </summary>
        /// <param name="sleepSpan">1ループ後にタイキする時間(ms)</param>
        /// <param name="cancelToken">キャンセルトークン</param>
        private async void LoopAnalyze(int sleepSpan, CancellationToken cancelToken)
        {
            try
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
            }catch (Exception e)
            {
                CommonMethods.ToastSimpleMessage("解析の途中でエラーが発生しました。",e.Message,MessageType.Error);
            }
        }

        /// <summary>
        /// OCRを使って画面を解析し、その結果と全イベントデータとの類似度(レーベンシュタイン距離)が最も大きいもの表示する。
        /// </summary>
        /// <returns></returns>
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
            ocrTexts = new string[GrobalValues.appSettings.ocrRangesDicList["twoChoise"].Length];

            // まずは画面キャプチャから
            int i=0;
            foreach (var range in GrobalValues.appSettings.ocrRangesDicList["twoChoise"])
            {
                cLeft = GrobalValues.appSettings.clipRangeDic["twoChoise"].left;
                cTop = GrobalValues.appSettings.clipRangeDic["twoChoise"].top;
                cRight = GrobalValues.appSettings.clipRangeDic["twoChoise"].right;
                cBottom = GrobalValues.appSettings.clipRangeDic["twoChoise"].buttom;

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

        /// <summary>
        /// 指定した範囲にある文字列をOCRで解析し、文字列で返す
        /// </summary>
        /// <param name="left">左端</param>
        /// <param name="top">上端</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>OCRした文字列</returns>
        private async Task<String> ImageRangeToString(int left = -1, int top = -1, int width = -1, int height = -1)
        {
            var bmp = screenShoter.GetCurrentSoftwareBitMap(left, top, width, height);
            var ocr = await this.BmpToString(bmp);
            return ocr.Text;
        }

        /// <summary>
        /// ビットマップをOCRし、結果の文字列を返す
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private async Task<OcrResult> BmpToString(SoftwareBitmap image)
        {
            OcrEngine ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            // OCR実行
            var ocrResult = await ocrEngine.RecognizeAsync(image);
            return ocrResult;
        }

        /// <summary>
        /// スクリーンキャプチャして、保存する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// スクリーンショットボタンクリック時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// ウィンドウ選択ボタンクリック時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SelectWindow(object sender, RoutedEventArgs e)
        {

            if (!screenShoter.isAvarableScreenShot())
            {
                var ryaku = previewPanel.TransformToVisual(mainGrid).TransformPoint(new Windows.Foundation.Point(0, 0));

                // スクリーンショットを投影する位置を調整
                var pos = new Vector3((float)ryaku.X, (float)ryaku.Y, 1f);
                var siz = previewPanel.ActualSize;

                screenShoter.Setup(this,
                        pos,
                        siz
                    );
            }
            // スクリーンショットの名前にウィンドウ名を使うので、ファイル名に使用できない文字の排除
            var windowName = CommonMethods.RemoveInvalidFileChar(await screenShoter.PickWindow());

            screenShoter.StartCaptureAsync();
            WriteConsole("ウィンドウ補足 : " + windowName);

        }

        /// <summary>
        /// 終了時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_Suspending(Object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            screenShoter.StopCapture();

            // ここでappデータの保存をする
        }

        /// <summary>
        /// 開始時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_Resuming(object sender, object e)
        {
            // ここでappデータの読み込みをする
        }

        /// <summary>
        /// 選択肢の表を更新する
        /// </summary>
        /// <param name="eventTitle">イベント名</param>
        /// <param name="choice1">選択肢1</param>
        /// <param name="effect1">効果1</param>
        /// <param name="choice2">選択肢2</param>
        /// <param name="effect2">効果2</param>
        private void updateChoice(string eventTitle, string choice1, string effect1, string choice2, string effect2) {
            EditTextBlockSync(this.eventTitle, eventTitle);
            EditTextBlockSync(this.choice1, choice1);
            EditTextBlockSync(this.effect1, effect1);
            EditTextBlockSync(this.choice2, choice2);
            EditTextBlockSync(this.effect2, effect2);
        }

        /// <summary>
        /// テキストブロックを非同期的に編集する
        /// 編集が競合しても勝手に解決してくれる
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

        /// <summary>
        /// コンソールにメッセージを表示する
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="type">メッセージの種別</param>
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
