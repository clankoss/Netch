using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netch;
using Netch.Servers;
using Netch.Utils;

namespace UnitTest
{
    [TestClass]
    public class UiSmoke
    {
        [TestMethod]
        public void ExistingGroupedNodeCanBeEditedAndSaved()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var node = new TrojanServer { Group = "Legacy subscription", Hostname = "old.example", Port = 443, Password = "test" };
                    using var form = new TrojanForm(node);
                    form.Shown += (_, _) =>
                    {
                        try
                        {
                            var input = (TextBox)form.Controls.Find("AddressTextBox", true)[0];
                            Assert.IsTrue(input.Enabled);
                            Assert.IsFalse(input.ReadOnly);
                            input.Focus();
                            Assert.IsTrue(input.Focused);
                            input.SelectedText = "new.example";
                            input.Text = "new.example";
                            ((Button)form.Controls.Find("SaveButton", true)[0]).PerformClick();
                        }
                        catch (Exception ex)
                        {
                            failure = ex;
                            form.Close();
                        }
                    };
                    Assert.AreEqual(DialogResult.OK, form.ShowDialog());
                    Assert.AreEqual("new.example", node.Hostname);
                    Global.Settings.Server.Remove(node);
                    Assert.AreEqual(0, Global.MainForm.ModeComboBox.Items.Count);
                }
                catch (Exception ex) { failure ??= ex; }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "UI smoke test did not finish");
            if (failure != null)
                throw failure;
        }

        [TestMethod]
        public void OnlyEnglishAndSimplifiedChineseAreAvailable()
        {
            CollectionAssert.AreEqual(new[] { "en-US", "zh-CN" }, i18N.GetTranslateList());
            i18N.Load("zh-TW");
            Assert.AreEqual("zh-CN", i18N.LangCode);
            Assert.AreEqual("打开 JSON 文件", i18N.Translate("Open JSON File"));
            i18N.Load("ja-JP");
            Assert.AreEqual("en-US", i18N.LangCode);
            Assert.AreEqual("Open JSON File", i18N.Translate("Open JSON File"));
        }
    }
}
