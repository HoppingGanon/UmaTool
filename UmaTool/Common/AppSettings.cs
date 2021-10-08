using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UmaTool.Common
{
    class AppSettings
    {
        //ファイル名
        public static string AppSettingsPath = "Assets/AppSettings.json";

        /// <summary>
        /// 設定ファイルを読み込んで、JSONオブジェクトをAppSettingsにデシリアライズする
        /// </summary>
        public static AppSettings loadAppSettings()
        {
            try
            {
                //ファイルをビルドに含めるには、ファイルのプロパティから「出力ディレクトリにコピー」等を設定する必要あり
                return JsonConvert.DeserializeObject<AppSettings>(BaseCommonMethods.getContent(AppSettingsPath));
            }
            catch (Exception e)
            {
                BaseCommonMethods.ToastSimpleMessage($"'{AppSettingsPath}'が読み込めませんでした", e.Message, MessageType.Error);
                return new AppSettings();
            }
        }

        /// <summary>
        /// ログファイルの出力名
        /// </summary>
        public string logFileName = "noname.log";

        /// <summary>
        /// ログファイルの保存世代数
        /// </summary>
        public int logMaxLines = 255;

        /// <summary>
        /// OCRで読み取る範囲をもった配列
        /// </summary>
        public Dictionary<String, RelativeRange[]> ocrRangesDicList = new Dictionary<String, RelativeRange[]>();
        
        /// <summary>
        /// OCRするときに除くウィンドウ枠の分の厚さ
        /// </summary>
        public Dictionary<String, OuterRange> clipRangeDic = new Dictionary<String, OuterRange>();

        /// <summary>
        /// ここに指定した数より、イベント選択肢の文字列の合計が少なければスキップする
        /// </summary>
        public int minEventStrLength = 5;

        /// <summary>
        /// OCR認識文字列のしきい率
        /// </summary>
        public double defDistRate = 0.7;
    }

    class RelativeRange
    {
        public double top = 0;
        public double left = 0;
        public double width = 0;
        public double height = 0;
    }

    class OuterRange
    {
        public int top = 0;
        public int left = 0;
        public int right = 0;
        public int buttom = 0;
    }
}
