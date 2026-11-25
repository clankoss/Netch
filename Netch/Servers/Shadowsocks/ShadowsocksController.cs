using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Netch.Controllers;
using Netch.Interfaces;
using Netch.Models;

namespace Netch.Servers
{
    internal sealed class ShadowsocksDispatchController : IServerController
    {
        private IServerController? _controller;

        public string Name => _controller?.Name ?? "Shadowsocks";
        public ushort? Socks5LocalPort { get; set; }
        public string? LocalAddress { get; set; }

        public Task<Socks5LocalServer> StartAsync(Server server)
        {
            var shadowsocks = (ShadowsocksServer)server;
            _controller = shadowsocks.EncryptMethod.StartsWith("2022-", System.StringComparison.Ordinal)
                ? new SingBoxController()
                : new ShadowsocksController();
            _controller.Socks5LocalPort = Socks5LocalPort;
            _controller.LocalAddress = LocalAddress;
            return _controller.StartAsync(server);
        }

        public Task StopAsync() => _controller?.StopAsync() ?? Task.CompletedTask;
    }

    public class ShadowsocksController : Guard, IServerController
    {
        public ShadowsocksController() : base("Shadowsocks.exe")
        {
        }

        protected override IEnumerable<string> StartedKeywords => new[] { "listening at" };

        protected override IEnumerable<string> FailedKeywords => new[] { "Invalid config path", "usage", "plugin service exit unexpectedly" };

        public override string Name => "Shadowsocks";

        public ushort? Socks5LocalPort { get; set; }

        public string? LocalAddress { get; set; }

        public async Task<Socks5LocalServer> StartAsync(Server s)
        {
            var server = (ShadowsocksServer)s;

            var arguments = new object?[]
            {
                "-s", await server.AutoResolveHostnameAsync(),
                "-p", server.Port,
                "-b", this.LocalAddress(),
                "-l", this.Socks5LocalPort(),
                "-m", server.EncryptMethod,
                "-k", server.Password,
                "-u", SpecialArgument.Flag,
                "--plugin", server.Plugin,
                "--plugin-opts", server.PluginOption
            };

            await StartGuardAsync(Arguments.Format(arguments));
            return new Socks5LocalServer(IPAddress.Loopback.ToString(), this.Socks5LocalPort(), server.Hostname);
        }
    }
}