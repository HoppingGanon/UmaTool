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
        public string logFileName = "noname.log";
        public int logMaxLines = 255;

        public Dictionary<String, RelativeRange[]> ocrRangesDic = new Dictionary<String, RelativeRange[]>();
        public Dictionary<String, OuterRange> clipRangeDic = new Dictionary<String, OuterRange>();

        public int minEventStrLength = 5;
        public double defDistRate = 0.7;

        /// <summary>
        /// 設定ファイルを読み込んで、JSONオブジェクトをAppSettingsにデシリアライズする
        /// </summary>
        public static void loadAppSettings()
        {
            try
            {
                //ファイルをビルドに含めるには、ファイルのプロパティから「出力ディレクトリにコピー」等を設定する必要あり
                const string AppSettingsPath = "Assets/AppSettings.json";
                GrobalValues.appSettings = JsonConvert.DeserializeObject<AppSettings>(BaseCommonMethods.getContent(AppSettingsPath));
            }
            catch (Exception e)
            {
                GrobalValues.appSettings = new AppSettings();
                BaseCommonMethods.ToastSimpleMessage("'AppSettings.json'が読み込めませんでした", e.Message, MessageType.Error);
            }
        }
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
