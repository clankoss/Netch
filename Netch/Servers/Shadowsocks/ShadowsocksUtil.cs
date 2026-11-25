using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Netch.Interfaces;
using Netch.Models;
using Netch.Utils;
using Serilog;

namespace Netch.Servers
{
    public class ShadowsocksUtil : IServerUtil
    {
        public ushort Priority { get; } = 1;

        public string TypeName { get; } = "SS";

        public string FullName { get; } = "Shadowsocks";

        public string ShortName { get; } = "SS";

        public string[] UriScheme { get; } = { "ss", "ssd" };

        public Type ServerType { get; } = typeof(ShadowsocksServer);

        public void Edit(Server s)
        {
            using var form = new ShadowsocksForm((ShadowsocksServer)s);
            form.ShowDialog();
        }

        public void Create()
        {
            using var form = new ShadowsocksForm();
            form.ShowDialog();
        }

        public string GetShareLink(Server s)
        {
            var server = (ShadowsocksServer)s;
            var userInfo = server.EncryptMethod.StartsWith("2022-", StringComparison.Ordinal)
                ? $"{Uri.EscapeDataString(server.EncryptMethod)}:{Uri.EscapeDataString(server.Password)}"
                : ShareLink.URLSafeBase64Encode($"{server.EncryptMethod}:{server.Password}");
            var host = server.Hostname.Contains(':') ? $"[{server.Hostname.Trim('[', ']')}]" : server.Hostname;
            var plugin = string.IsNullOrWhiteSpace(server.Plugin) ? "" :
                $"/?plugin={Uri.EscapeDataString(string.IsNullOrEmpty(server.PluginOption) ? server.Plugin : $"{server.Plugin};{server.PluginOption}")}";
            return $"ss://{userInfo}@{host}:{server.Port}{plugin}#{Uri.EscapeDataString(server.Remark)}";
        }

        public IServerController GetController()
        {
            return new ShadowsocksDispatchController();
        }

        public IEnumerable<Server> ParseUri(string text)
        {
            if (text.StartsWith("ss://"))
                return new[] { ParseSsUri(text) };

            if (text.StartsWith("ssd://"))
                return ParseSsdUri(text);

            throw new FormatException();
        }

        public bool CheckServer(Server s)
        {
            var server = (ShadowsocksServer)s;
            if (!SSGlobal.EncryptMethods.Contains(server.EncryptMethod))
            {
                Log.Warning("不支持的 SS 加密方式：{Method}", server.EncryptMethod);
                return false;
            }

            return true;
        }

        public IEnumerable<Server> ParseSsdUri(string s)
        {
            var json = JsonSerializer.Deserialize<SSDJObject>(ShareLink.URLSafeBase64Decode(s.Substring(6)))!;

            return json.servers.Select(server => new ShadowsocksServer
                {
                    Remark = server.remarks,
                    Hostname = server.server,
                    Port = server.port != 0 ? server.port : json.port,
                    Password = server.password ?? json.password,
                    EncryptMethod = server.encryption ?? json.encryption,
                    Plugin = string.IsNullOrEmpty(json.plugin) ? string.IsNullOrEmpty(server.plugin) ? null : server.plugin : json.plugin,
                    PluginOption = string.IsNullOrEmpty(json.plugin_options)
                        ? string.IsNullOrEmpty(server.plugin_options) ? null : server.plugin_options
                        : json.plugin_options
                })
                .Where(CheckServer);
        }

        public ShadowsocksServer ParseSsUri(string text)
        {
            var data = new ShadowsocksServer();

            var fragmentStart = text.IndexOf('#');
            if (fragmentStart >= 0)
            {
                data.Remark = HttpUtility.UrlDecode(text[(fragmentStart + 1)..]);
                text = text[..fragmentStart];
            }

            var queryStart = text.IndexOf('?');
            if (queryStart >= 0)
            {
                var plugins = HttpUtility.ParseQueryString(text[(queryStart + 1)..]).Get("plugin");
                if (plugins != null)
                {
                    var parts = plugins.Split(';', 2);
                    var plugin = parts[0];
                    var pluginopts = parts.Length == 2 ? parts[1] : "";
                    switch (plugin)
                    {
                        case "obfs-local":
                        case "simple-obfs":
                            plugin = "simple-obfs";
                            if (!pluginopts.Contains("obfs="))
                                pluginopts = "obfs=http;obfs-host=" + pluginopts;

                            break;
                        case "simple-obfs-tls":
                            plugin = "simple-obfs";
                            if (!pluginopts.Contains("obfs="))
                                pluginopts = "obfs=tls;obfs-host=" + pluginopts;

                            break;
                    }

                    data.Plugin = plugin;
                    data.PluginOption = pluginopts;
                }

                text = text[..queryStart].TrimEnd('/');
            }

            if (text.Contains("@"))
            {
                var finder = new Regex(@"^ss://(?<base64>.+?)@(?<server>.+):(?<port>\d+)");
                var parser = new Regex(@"^(?<method>.+?):(?<password>.+)$");
                var match = finder.Match(text);
                if (!match.Success)
                    throw new FormatException();

                data.Hostname = match.Groups["server"].Value.Trim('[', ']');
                data.Port = ushort.Parse(match.Groups["port"].Value);

                var userInfo = Uri.UnescapeDataString(match.Groups["base64"].Value);
                if (!userInfo.Contains(':'))
                    userInfo = ShareLink.URLSafeBase64Decode(userInfo);
                match = parser.Match(userInfo);
                if (!match.Success)
                    throw new FormatException();

                data.EncryptMethod = match.Groups["method"].Value;
                data.Password = match.Groups["password"].Value;
            }
            else
            {
                var parser = new Regex(@"^((?<method>.+?):(?<password>.+)@(?<server>.+):(?<port>\d+))");
                var match = parser.Match(ShareLink.URLSafeBase64Decode(text.Replace("ss://", "")));
                if (!match.Success)
                    throw new FormatException();

                data.Hostname = match.Groups["server"].Value.Trim('[', ']');
                data.Port = ushort.Parse(match.Groups["port"].Value);
                data.EncryptMethod = match.Groups["method"].Value;
                data.Password = match.Groups["password"].Value;
            }

            return CheckServer(data) ? data : throw new FormatException();
        }
    }
}