using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netch.Servers;
using Netch.Utils;

namespace UnitTest
{
    [TestClass]
    public class ShadowsocksImportTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ClipboardAcceptsV2rayNLinksWithEscapedBase64Padding(bool base64List)
        {
            const string method = "aes-128-gcm";
            const string password = "p:a+/ss";
            var userInfo = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{method}:{password}"))
                .Replace('+', '-').Replace('/', '_');
            Assert.IsTrue(userInfo.EndsWith('='));
            var link = $"ss://{Uri.EscapeDataString(userInfo)}@example.com:8388#Game+SS";
            var clipboard = base64List ? Convert.ToBase64String(Encoding.UTF8.GetBytes(link + "\r\n")) : link;
            var server = (ShadowsocksServer)ShareLink.ParseText("\uFEFF" + clipboard + "\r\n").Single();

            Assert.AreEqual(method, server.EncryptMethod);
            Assert.AreEqual(password, server.Password);
            Assert.AreEqual("example.com", server.Hostname);
            Assert.AreEqual((ushort)8388, server.Port);
            Assert.AreEqual("Game SS", server.Remark);
        }

        [TestMethod]
        public void ClipboardDecodesPlainCredentialsExactlyOnce()
        {
            const string link = "ss://2022-blake3-aes-128-gcm%3Aone%3Atwo%252F%2B%3D@127.0.0.1:8388";
            var server = (ShadowsocksServer)ShareLink.ParseText(link).Single();
            Assert.AreEqual("one:two%2F+=", server.Password);
        }
    }
}
