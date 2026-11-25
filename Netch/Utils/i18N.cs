using Netch.Properties;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Netch.Utils
{
    public static class i18N
    {
#if NET
        static i18N()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
#endif

        /// <summary>
        ///     数据
        /// </summary>
        public static Hashtable Data = new();

        public static string LangCode { get; private set; } = "en-US";

        /// <summary>
        ///     加载
        /// </summary>
        /// <param name="value">语言代码</param>
        public static void Load(string value)
        {
            var culture = string.IsNullOrEmpty(value) || value == "System"
                ? CultureInfo.CurrentUICulture.Name
                : value;
            LangCode = culture == "zh" || culture.StartsWith("zh-", System.StringComparison.OrdinalIgnoreCase)
                ? "zh-CN"
                : "en-US";

            Data = new Hashtable();
            if (LangCode == "zh-CN")
            {
                var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(Resources.zh_CN);
                if (dictionary != null)
                    foreach (var pair in dictionary)
                        Data.Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        ///     翻译
        /// </summary>
        /// <param name="text">需要翻译的文本</param>
        /// <returns>翻译完毕的文本</returns>
        public static string Translate(params object[] text)
        {
            var a = new StringBuilder();
            foreach (var t in text)
                if (t is string)
                    a.Append(Data[t]?.ToString() ?? t);
                else
                    a.Append(t);

            return a.ToString();
        }

        public static string TranslateFormat(string format, params object[] args)
        {
            for (var i = 0; i < args.Length; i++)
                if (args[i] is string)
                    args[i] = Translate((string)args[i]);

            return string.Format(Translate(format), args);
        }

        public static List<string> GetTranslateList() => new() { "en-US", "zh-CN" };

        public static void TranslateForm(in Control c)
        {
            Utils.ComponentIterator(c,
                component =>
                {
                    switch (component)
                    {
                        case TextBoxBase:
                        case ListControl:
                            break;
                        case Control control:
                            control.Text = Translate(control.Text);
                            break;
                        case ToolStripItem toolStripItem:
                            toolStripItem.Text = Translate(toolStripItem.Text);
                            break;
                        case ColumnHeader columnHeader:
                            columnHeader.Text = Translate(columnHeader.Text);
                            break;
                    }
                });
        }
    }
}