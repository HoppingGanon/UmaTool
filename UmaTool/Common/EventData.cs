using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using System.IO;

namespace UmaTool.Common
{
    /// <summary>
    /// EventData.jsonをクラスとしてもつ
    /// </summary>
    class EventData
    {
        // イベントデータのファイル名
        public static string fileName = "EventData.json";

        /// <summary>
        /// 静的メソッドで、EventData.jsonをシリアライズする
        /// </summary>
        /// <returns>読み取ったEventData配列</returns>
        public static EventData[] GetEventDataList()
        {
            StorageFile file;
            try
            {
                // ファイルを取得
                var task = ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                while (task.Status == Windows.Foundation.AsyncStatus.Started)
                {
                    //処理が終了するまで待機
                    Task.Delay(50);
                }
                file = task.GetResults();

                //ファイルをビルドに含めるには、ファイルのプロパティから「出力ディレクトリにコピー」等を設定する必要あり
                return JsonConvert.DeserializeObject<EventData[]>(BaseCommonMethods.getContent(file.Path));
            }
            catch (FileNotFoundException e)
            {
                BaseCommonMethods.ToastSimpleMessage($"'{fileName}'が読み込めませんでした", "ファイルがありません", MessageType.Error);
            }
            catch (Exception e)
            {
                BaseCommonMethods.ToastSimpleMessage($"'{fileName}'が読み込めませんでした", e.Message, MessageType.Error);
            }
            return new EventData[0];
        }

        /// <summary>
        /// イベントのタイトル
        /// </summary>
        public string title;
        /// <summary>
        /// イベント分類
        /// </summary>
        public string category;
        /// <summary>
        /// イベントが発生するキャラ名
        /// </summary>
        public string name;
        /// <summary>
        /// 選択肢配列
        /// </summary>
        public ChoiceData[] choices;
    }

    /// <summary>
    /// 選択肢クラス
    /// </summary>
    class ChoiceData
    {
        /// <summary>
        /// 選択肢ふとつぶんのテキスト
        /// </summary>
        public string text;
        /// <summary>
        /// 効果を示すテキスト
        /// </summary>
        public string effect;
    }
}
