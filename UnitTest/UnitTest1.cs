using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netch;
using Netch.Servers;

namespace UnitTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task RealityVisionLinkSurvivesSharingAndGeneratesXraySettingsAsync()
        {
            const string link = "vless://18f97adc-20e1-49e3-9c82-03af63d5667c@127.0.0.1:443"
                + "?type=raw&security=reality&flow=xtls-rprx-vision&sni=example.com&fp=chrome&pbk=key&sid=abcd&spx=%2F%2520";
            var server = (VLESSServer)V2rayUtils.ParseVUri(link).Single();
            var shared = (VLESSServer)V2rayUtils.ParseVUri(V2rayUtils.GetVShareLink(server, "vless")).Single();
            Assert.AreEqual("/%20", shared.RealitySpiderX);

            var config = await V2rayConfigUtils.GenerateClientConfigAsync(shared);
            var outbound = config.outbounds.Single();
            Assert.AreEqual("xtls-rprx-vision", outbound.settings.vnext.Single().users.Single().flow);
            Assert.IsFalse(outbound.mux.enabled);

            var stream = JsonSerializer.SerializeToElement(outbound.streamSettings, Global.NewCustomJsonSerializerOptions());
            Assert.AreEqual("key", stream.GetProperty("realitySettings").GetProperty("password").GetString());
            Assert.AreEqual("example.com", stream.GetProperty("realitySettings").GetProperty("serverName").GetString());
        }

        [TestMethod]
        public void Shadowsocks2022LinkPreservesPercentEncodedKey()
        {
            const string link = "ss://2022-blake3-aes-128-gcm:%2B%2F%3D%3D@127.0.0.1:443#Example";
            var util = new ShadowsocksUtil();
            var server = (ShadowsocksServer)util.ParseUri(link).Single();
            Assert.AreEqual("+/==", server.Password);
            Assert.AreEqual(link, util.GetShareLink(server));
        }

        [TestMethod]
        public async Task TrojanDefaultsToTlsAndKeepsSniAsync()
        {
            var util = new TrojanUtil();
            var server = (TrojanServer)util.ParseUri("trojan://test@127.0.0.1:443?sni=example.com").Single();
            var config = await V2rayConfigUtils.GenerateClientConfigAsync(server);
            var outbound = config.outbounds.Single();
            Assert.AreEqual("trojan", outbound.protocol);
            Assert.AreEqual("tls", outbound.streamSettings.security);
            Assert.AreEqual("example.com", outbound.streamSettings.tlsSettings.serverName);
        }
    }
}
