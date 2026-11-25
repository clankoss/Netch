using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netch.Forms;

namespace UnitTest
{
    [TestClass]
    public class ToolbarSmoke
    {
        [TestMethod]
        public void MainToolbarLaysOutAndDrawsAllActions()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new MainForm();
                    typeof(MainForm).GetMethod("TranslateControls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(form, null);
                    form.CreateControl();
                    form.PerformLayout();
                    var names = new[] { "EditServerButton", "DeleteServerButton", "SpeedButton", "CopyLinkButton",
                        "EditModeButton", "DeleteModeButton" };
                    foreach (var name in names)
                    {
                        var button = (ActionIconButton)form.Controls.Find(name, true).Single();
                        button.Parent!.PerformLayout();
                        Assert.IsTrue(button.Width is >= 24 and <= 34 && button.Height is >= 20 and <= 34, name + " size " + button.Size);
                        Assert.IsTrue(button.TabStop && !string.IsNullOrEmpty(button.AccessibleName));
                        using var bitmap = new Bitmap(button.Width, button.Height);
                        button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        var distinctPixels = Enumerable.Range(0, bitmap.Width)
                            .SelectMany(x => Enumerable.Range(0, bitmap.Height).Select(y => bitmap.GetPixel(x, y).ToArgb()))
                            .Distinct().Count();
                        Assert.IsTrue(distinctPixels > 2, name + " did not draw a glyph");
                    }
                    Assert.AreEqual(0, form.Controls.Find("ProfileGroupBox", true).Length);
                    var menu = (MenuStrip)form.Controls.Find("MenuStrip", true).Single();
                    Assert.AreEqual(14, menu.Items["VersionLabel"]!.Margin.Right);
                }
                catch (Exception ex) { failure = ex; }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(8)));
            if (failure != null)
                throw failure;
        }
    }
}
