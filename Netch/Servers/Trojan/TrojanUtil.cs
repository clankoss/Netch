using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using Netch.Interfaces;
using Netch.Models;

namespace Netch.Servers
{
    public class TrojanUtil : IServerUtil
    {
        public ushort Priority { get; } = 3;

        public string TypeName { get; } = "Trojan";

        public string FullName { get; } = "Trojan";

        public string ShortName { get; } = "TR";

        public string[] UriScheme { get; } = { "trojan" };

        public Type ServerType { get; } = typeof(TrojanServer);

        public void Edit(Server s)
        {
            using var form = new TrojanForm((TrojanServer)s);
            form.ShowDialog();
        }

        public void Create()
        {
            using var form = new TrojanForm();
            form.ShowDialog();
        }

        public string GetShareLink(Server s)
        {
            var server = (TrojanServer)s;
            var host = server.Hostname.Contains(':') ? $"[{server.Hostname.Trim('[', ']')}]" : server.Hostname;
            var sni = string.IsNullOrWhiteSpace(server.Host) ? "" : $"?sni={Uri.EscapeDataString(server.Host)}";
            return $"trojan://{Uri.EscapeDataString(server.Password)}@{host}:{server.Port}{sni}#{Uri.EscapeDataString(server.Remark)}";
        }

        public IServerController GetController()
        {
            return new V2rayController();
        }

        public IEnumerable<Server> ParseUri(string text)
        {
            var data = new TrojanServer();

            text = text.Replace("/?", "?");
            if (text.Contains("#"))
            {
                data.Remark = HttpUtility.UrlDecode(text.Split('#')[1]);
                text = text.Split('#')[0];
            }

            if (text.Contains("?"))
            {
                var reg = new Regex(@"^(?<data>.+?)\?(.+)$");
                var regmatch = reg.Match(text);

                if (!regmatch.Success)
                    throw new FormatException();

                var parameters = HttpUtility.ParseQueryString(new Uri(text).Query);
                var peer = parameters.Get("sni") ?? parameters.Get("peer");

                if (peer != null)
                    data.Host = peer;

                text = regmatch.Groups["data"].Value;
            }

            var finder = new Regex(@"^trojan://(?<psk>.+?)@(?<server>.+):(?<port>\d+)");
            var match = finder.Match(text);
            if (!match.Success)
                throw new FormatException();

            data.Password = HttpUtility.UrlDecode(match.Groups["psk"].Value);
            data.Hostname = match.Groups["server"].Value.Trim('[', ']');
            data.Port = ushort.Parse(match.Groups["port"].Value);

            return new[] { data };
        }

        public bool CheckServer(Server s)
        {
            return true;
        }
    }
}