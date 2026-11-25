using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Netch.Models;
using Netch.Utils;

namespace Netch.Servers
{
    public static class V2rayUtils
    {
        public static IEnumerable<Server> ParseVUri(string text)
        {
            var scheme = ShareLink.GetUriScheme(text).ToLower();
            var server = scheme switch { "vmess" => new VMessServer(), "vless" => new VLESSServer(), _ => throw new ArgumentOutOfRangeException() };
            if (text.Contains("#"))
            {
                server.Remark = Uri.UnescapeDataString(text.Split('#')[1]);
                text = text.Split('#')[0];
            }

            if (text.Contains("?"))
            {
                var parameter = HttpUtility.ParseQueryString(text.Split('?')[1]);
                text = text.Substring(0, text.IndexOf("?", StringComparison.Ordinal));
                server.TransferProtocol = parameter.Get("type") ?? "tcp";
                server.EncryptMethod = parameter.Get("encryption") ?? scheme switch { "vless" => "none", _ => "auto" };
                switch (server.TransferProtocol)
                {
                    case "raw":
                    case "tcp":
                        server.FakeType = parameter.Get("headerType") ?? "none";
                        server.Host = parameter.Get("host");
                        server.Path = parameter.Get("path");
                        break;
                    case "kcp":
                        server.FakeType = parameter.Get("headerType") ?? "none";
                        server.Path = parameter.Get("seed") ?? "";
                        break;
                    case "xhttp":
                        server.XHttpMode = parameter.Get("mode");
                        goto case "ws";
                    case "httpupgrade":
                    case "ws":
                        server.Path = parameter.Get("path") ?? "/";
                        server.Host = parameter.Get("host") ?? "";
                        break;
                    case "h2":
                        server.Path = parameter.Get("path") ?? "/";
                        server.Host = parameter.Get("host") ?? "";
                        break;
                    case "quic":
                        server.QUICSecure = parameter.Get("quicSecurity") ?? "none";
                        server.QUICSecret = parameter.Get("key") ?? "";
                        server.FakeType = parameter.Get("headerType") ?? "none";
                        break;
                    case "grpc":
                        server.FakeType = parameter.Get("mode") ?? "gun";
                        server.Path = parameter.Get("serviceName") ?? "";
                        break;
                }

                server.TLSSecureType = parameter.Get("security") ?? "none";
                if (server.TLSSecureType != "none")
                {
                    server.ServerName = parameter.Get("sni") ?? "";
                    if (server is VLESSServer vless)
                    {
                        vless.Flow = parameter.Get("flow");
                        vless.Fingerprint = parameter.Get("fp");
                        vless.RealityPublicKey = parameter.Get("pbk");
                        vless.RealityShortId = parameter.Get("sid");
                        vless.RealitySpiderX = parameter.Get("spx");
                    }
                }
            }

            var finder = new Regex(@$"^{scheme}://(?<guid>.+?)@(?<server>.+):(?<port>\d+)");
            var match = finder.Match(text.Split('?')[0]);
            if (!match.Success)
                throw new FormatException();

            server.UserID = match.Groups["guid"].Value;
            server.Hostname = match.Groups["server"].Value.Trim('[', ']');
            server.Port = ushort.Parse(match.Groups["port"].Value);

            return new[] { server };
        }

        public static string GetVShareLink(Server s, string scheme = "vmess")
        {
            // https://github.com/XTLS/Xray-core/issues/91
            var server = (VMessServer)s;
            var parameter = new Dictionary<string, string>();
            // protocol-specific fields
            parameter.Add("type", server.TransferProtocol);
            parameter.Add("encryption", server.EncryptMethod);

            // transport-specific fields
            switch (server.TransferProtocol)
            {
                case "tcp":
                    break;
                case "kcp":
                    if (server.FakeType != "none")
                        parameter.Add("headerType", server.FakeType);

                    if (!server.Path.IsNullOrWhiteSpace())
                        parameter.Add("seed", Uri.EscapeDataString(server.Path!));

                    break;
                case "xhttp":
                    if (!string.IsNullOrWhiteSpace(server.XHttpMode))
                        parameter.Add("mode", Uri.EscapeDataString(server.XHttpMode));
                    goto case "ws";
                case "httpupgrade":
                case "ws":
                    parameter.Add("path", Uri.EscapeDataString(server.Path.ValueOrDefault() ?? "/"));
                    if (!server.Host.IsNullOrWhiteSpace())
                        parameter.Add("host", Uri.EscapeDataString(server.Host!));

                    break;
                case "h2":
                    parameter.Add("path", Uri.EscapeDataString(server.Path.ValueOrDefault() ?? "/"));
                    if (!server.Host.IsNullOrWhiteSpace())
                        parameter.Add("host", Uri.EscapeDataString(server.Host!));

                    break;
                case "quic":
                    if (server.QUICSecure is not (null or "none"))
                    {
                        parameter.Add("quicSecurity", server.QUICSecure);
                        parameter.Add("key", server.QUICSecret!);
                    }

                    if (server.FakeType != "none")
                        parameter.Add("headerType", server.FakeType);

                    break;
                case "grpc":
                    if (!string.IsNullOrEmpty(server.Path))
                        parameter.Add("serviceName", server.Path);

                    if (server.FakeType is "gun" or "multi")
                        parameter.Add("mode", server.FakeType);

                    break;
            }

            if (server.TLSSecureType != "none")
            {
                parameter.Add("security", server.TLSSecureType);

                if (!string.IsNullOrWhiteSpace(server.ServerName))
                    parameter.Add("sni", Uri.EscapeDataString(server.ServerName));

                if (server is VLESSServer vless)
                {
                    foreach (var (key, value) in new[]
                    {
                        ("flow", vless.Flow), ("fp", vless.Fingerprint), ("pbk", vless.RealityPublicKey),
                        ("sid", vless.RealityShortId), ("spx", vless.RealitySpiderX)
                    })
                        if (value != null)
                            parameter.Add(key, Uri.EscapeDataString(value));
                }
            }

            var host = server.Hostname.Contains(':') ? $"[{server.Hostname.Trim('[', ']')}]" : server.Hostname;
            return
                $"{scheme}://{server.UserID}@{host}:{server.Port}?{string.Join("&", parameter.Select(p => $"{p.Key}={p.Value}"))}{(!server.Remark.IsNullOrWhiteSpace() ? $"#{Uri.EscapeDataString(server.Remark)}" : "")}";
        }
    }
}