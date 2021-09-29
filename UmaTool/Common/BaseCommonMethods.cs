using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NotificationsExtensions.Toasts;
using Windows.UI.Notifications;
using Newtonsoft.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;


namespace UmaTool.Common
{
    public enum MessageType
    {
        Success,
        Information,
        Caution,
        Error,
        Custom
    }

    class BaseCommonMethods
    {

        /// <summary>
        /// 通知をトースト形式で出力する
        /// </summary>
        /// <param name="message">メインメッセージ</param>
        /// <param name="title">タイトル</param>
        /// <param name="detail">詳細メッセージ</param>
        public static void ToastSimpleMessage(string message, string detail = "", MessageType toastType = MessageType.Information, string customTitle = "", Boolean isWritingLog = true)
        {
            if (isWritingLog) { 
                //ついでにログにも書き込んじゃう
                try
                {
                    CommonMethods.appendLog($"{message} : {detail}", toastType, customTitle);
                }
                catch { }
            }

            string title;

            switch (toastType)
            {
                case MessageType.Success:
                    title = "成功";
                    break;
                case MessageType.Information:
                    title = "お知らせ";
                    break;
                case MessageType.Caution:
                    title = "警告";
                    break;
                case MessageType.Error:
                    title = "エラー";
                    break;
                case MessageType.Custom:
                    title = customTitle;
                    break;
                default:
                    title = "";
                    break;
            }

            ToastVisual visual = new ToastVisual
            {
                TitleText = new ToastText
                {
                    Text = title
                },
                BodyTextLine1 = new ToastText
                {
                    Text = message
                },
                BodyTextLine2 = new ToastText
                {
                    Text = detail
                }
            };

            ToastContent toast = new ToastContent
            {
                Visual = visual,
                ActivationType = ToastActivationType.Background,
                Duration = ToastDuration.Short
            };


            var doc = toast.GetXml();
            ToastNotification notification = new ToastNotification(doc);
            ToastNotificationManager.CreateToastNotifier().Show(notification);
        }

        /// <summary>
        /// 指定したファイルパスを文字列として取得
        /// </summary>
        /// <param name="path">ファイルパス</param>
        /// <returns>内容の文字列</returns>
        public static string getContent(string path) {
            string[] lines = System.IO.File.ReadAllLines(path);
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 実行場所の直下にテキストファイルとして文字列を一行書き込む
        /// </summary>
        /// <param name="fileName">書き込む対象のファイル名</param>
        /// <param name="line">書き込む内容</param>
        /// <param name="isAppend">Trueを指定すると追記</param>
        /// <param name="encoding">文字コード</param>
        /// <returns>なし</returns>
        public static async Task WriteLineLocal(string fileName,string line,Boolean isAppend = true, Windows.Storage.Streams.UnicodeEncoding encoding = Windows.Storage.Streams.UnicodeEncoding.Utf8)
        {
            StorageFile file;
            try
            {
                StorageFolder storageFolder = ApplicationData.Current.LocalFolder;

                try
                {
                    file = await storageFolder.GetFileAsync(fileName);
                }
                catch
                {
                    file = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.FailIfExists);
                }
                file = await storageFolder.GetFileAsync(fileName);
            }
            catch
            {
                BaseCommonMethods.ToastSimpleMessage("ログファイルの情報が取得できません", isWritingLog: false);
                return;
            }

            // 非同期で保存処理を行う
            _ = Task.Run(async () =>
              {
                  foreach (int i in Enumerable.Range(1, 10))
                  {
                      try
                      {
                          if (isAppend)
                          {
                              // 追記
                              await Windows.Storage.FileIO.AppendTextAsync(file, line);
                              return;
                          }
                          else
                          {
                              // 上書き
                              await Windows.Storage.FileIO.WriteTextAsync(file, line);
                              return;
                          }
                      }
                      catch
                      {
                          //エラー時は40ms停止して再試行(10回まで)
                          await Task.Delay(40);
                      }
                  }

                  BaseCommonMethods.ToastSimpleMessage("ログファイルの書き込みに失敗しました", isWritingLog: false);
              });

        }
        /// <summary>
        /// ログを追記するメソッド
        /// </summary>
        /// <param name="message">メッセージ本文</param>
        /// <param name="type">メッセージタイプ</param>
        /// <param name="customTitle">カスタムタイトル</param>
        public static void appendLog(string message, MessageType type = MessageType.Information, string customTitle = "")
        {
            string title;
            switch (type)
            {
                case MessageType.Success:
                    title = "成功";
                    break;

                case MessageType.Information:
                    title = "情報";
                    break;

                case MessageType.Caution:
                    title = "警告";
                    break;

                case MessageType.Error:
                    title = "エラー";
                    break;

                case MessageType.Custom:
                    title = customTitle;
                    break;

                default:
                    title = "";
                    break;

            }

            string rowString = $"{DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss")}\t{title}\t{message}\n";
            WriteLineLocal(GrobalValues.appSettings.logFileName, rowString);
        }
        public static string RemoveInvalidFileChar(string name) {

            return name.Replace("\\", "￥")
                .Replace("/", "／")
                .Replace(":", "：")
                .Replace("*", "＊")
                .Replace("?", "？")
                .Replace("\"", "”")
                .Replace("<", "＜")
                .Replace(">", "＞")
                .Replace("|", "｜");
        }

        public static string SubstringSafe(string str,int start,int length = -1) {
            start = Math.Max(Math.Min(str.Length - 1,start),0);
            length = Math.Min(Math.Max(length, 0), str.Length - start);
            return str.Substring(start,length);
        }

        public static SoftwareBitmap BytesToSoftwareBMP(Byte[] bytes, int width, int height)
        {
            return SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, width, height);
        }

        public static async Task<IRandomAccessStream> MemoryStreamToRandomAccessStream(MemoryStream memoryStream)
        {
            var randomAccessStream = new InMemoryRandomAccessStream();
            var outputStream = randomAccessStream.GetOutputStreamAt(0);
            var dw = new DataWriter(outputStream);
            var task = new Task(() => dw.WriteBytes(memoryStream.ToArray()));
            task.Start();
            await task;
            await dw.StoreAsync();
            await outputStream.FlushAsync();
            return randomAccessStream;
        }

        public static async void SaveSoftwareBitmap(SoftwareBitmap bmp, StorageFolder pathObject, string fileName) {

            await pathObject.TryGetItemAsync(fileName);
            var file = await pathObject.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
            var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, fileStream);

            // Set the software bitmap
            encoder.SetSoftwareBitmap(bmp);

            // Set additional encoding parameters, if needed
            encoder.BitmapTransform.ScaledWidth = (uint)bmp.PixelWidth;
            encoder.BitmapTransform.ScaledHeight = (uint)bmp.PixelHeight;
            encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.None;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;
            encoder.IsThumbnailGenerated = true;

            await encoder.FlushAsync();
        }

    }

}
