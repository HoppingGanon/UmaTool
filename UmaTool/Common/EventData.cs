using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UmaTool.Common
{
    /// <summary>
    /// EventData.jsonをクラスとしてもつ
    /// </summary>
    class EventData
    {
        /// <summary>
        /// 静的メソッドで、EventData.jsonをシリアライズする
        /// </summary>
        /// <returns>読み取ったEventData配列</returns>
        public static EventData[] GetEventDataList()
        {
            try
            {
                //ファイルをビルドに含めるには、ファイルのプロパティから「出力ディレクトリにコピー」等を設定する必要あり
                const string jsonPath = "Assets/EventData.json";
                return JsonConvert.DeserializeObject<EventData[]>(BaseCommonMethods.getContent(jsonPath));
            }
            catch (Exception e)
            {
                GrobalValues.appSettings = new AppSettings();
                BaseCommonMethods.ToastSimpleMessage("'EventData.json'が読み込めませんでした", e.Message, MessageType.Error);
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
