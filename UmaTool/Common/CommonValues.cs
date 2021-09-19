using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UmaTool.Common
{
    enum ToastType
    {
        Success,
        Information,
        Caution,
        Error,
        Custom
    }

    class CommonValues
    {
        public static AppSettings appSettings = new AppSettings();
    }
}
