using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UmaTool.Common
{
    /// <summary>
    /// 静的メンバ変数で、広域変数を保持する
    /// </summary>
    class GrobalValues
    {
        /// <summary>
        /// AppSettings.jsonから取得した設定情報
        /// </summary>
        public static AppSettings appSettings = new AppSettings();
    }
}
