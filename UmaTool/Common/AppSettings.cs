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
        public string version = "0.0.0";
        public string logFileName = "noname.log";
        public int logMaxLines = 255;

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
                BaseCommonMethods.ToastSimpleMessage("'AppSettings.json'が見つかりませんでした", e.Message, MessageType.Error);
            }
        }
    }
}
