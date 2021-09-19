using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NotificationsExtensions.Toasts;
using Windows.UI.Notifications;
using Newtonsoft.Json;

namespace UmaTool.Common
{
    class CommonMethods
    {
        /// <summary>
        /// 通知をトースト形式で出力する
        /// </summary>
        /// <param name="message">メインメッセージ</param>
        /// <param name="title">タイトル</param>
        /// <param name="detail">詳細メッセージ</param>
        public static void ToastSimpleMessage(String message, String detail = "", ToastType toastType = ToastType.Information, String customTitle = "")
        {

            String title;

            switch (toastType)
            {
                case ToastType.Success:
                    title = "成功";
                    break;
                case ToastType.Information:
                    title = "お知らせ";
                    break;
                case ToastType.Caution:
                    title = "警告";
                    break;
                case ToastType.Error:
                    title = "エラー";
                    break;
                case ToastType.Custom:
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
        /// 設定ファイルを読み込んで、JSONオブジェクトをAppSettingsにデシリアライズする
        /// </summary>
        public static void loadAppSettings() {
            try
            {
                //ファイルをビルドに含めるには、ファイルのプロパティから「出力ディレクトリにコピー」等を設定する必要あり
                const String AppSettingsPath = "Assets/AppSettings.json";
                CommonValues.appSettings = JsonConvert.DeserializeObject<AppSettings>(getContent(AppSettingsPath));
            }
            catch(Exception e)
            {
                CommonValues.appSettings = new AppSettings();
                ToastSimpleMessage("'AppSettings.json'が見つかりませんでした",e.Message,ToastType.Error);
            }
        }

        public static String getContent(String path) {
            string[] lines = System.IO.File.ReadAllLines(path);
            return String.Join("\n", lines);
        }


    }
}
