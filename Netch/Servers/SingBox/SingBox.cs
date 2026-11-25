using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Netch.Controllers;
using Netch.Interfaces;
using Netch.Models;
using Netch.Utils;

namespace Netch.Servers
{
    public class SingBoxServer : Server
    {
        public override string Type => "SingBox";
        public JsonElement Outbound { get; set; }
        public override string MaskedData() => "";
    }

    public class SingBoxUtil : IServerUtil
    {
        public ushort Priority => 20;
        public string TypeName => "SingBox";
        public string FullName => "sing-box JSON";
        public string ShortName => "SB";
        public string[] UriScheme => Array.Empty<string>();
        public Type ServerType => typeof(SingBoxServer);

        public void Create()
        {
            var server = new SingBoxServer();
            if (Load(server))
                Global.Settings.Server.Add(server);
        }

        public void Edit(Server server) => Load((SingBoxServer)server);

        public string GetShareLink(Server server) => ((SingBoxServer)server).Outbound.GetRawText();

        public IServerController GetController() => new SingBoxController();

        public IEnumerable<Server> ParseUri(string text) => Array.Empty<Server>();

        public bool CheckServer(Server server) => ((SingBoxServer)server).Outbound.ValueKind == JsonValueKind.Object;

        private static bool Load(SingBoxServer server)
        {
            using var form = new Form
            {
                Text = i18N.Translate("sing-box Outbound"),
                ClientSize = new Size(650, 445),
                MinimumSize = new Size(420, 300),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false,
                AutoScaleDimensions = new SizeF(96, 96),
                AutoScaleMode = AutoScaleMode.Dpi
            };
            var json = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Text = server.Outbound.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Serialize(server.Outbound, new JsonSerializerOptions { WriteIndented = true })
                    : ""
            };
            var description = new Label
            {
                Text = i18N.Translate("Paste a single outbound JSON object."),
                Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8)
            };
            var import = new Button { Text = i18N.Translate("Open JSON File"), AutoSize = true };
            var save = new Button { Text = i18N.Translate("Save"), AutoSize = true };
            var cancel = new Button { Text = i18N.Translate("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
            actions.Controls.AddRange(new Control[] { import, save, cancel });
            form.Controls.Add(json);
            form.Controls.Add(description);
            form.Controls.Add(actions);
            form.CancelButton = cancel;
            form.ActiveControl = json;

            string? fileName = null;
            import.Click += (_, _) =>
            {
                using var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
                if (dialog.ShowDialog(form) != DialogResult.OK)
                    return;
                try
                {
                    json.Text = File.ReadAllText(dialog.FileName);
                    fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show(form, ex.Message, form.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            save.Click += (_, _) =>
            {
                try
                {
                    using var document = JsonDocument.Parse(json.Text);
                    var outbound = document.RootElement;
                    if (outbound.ValueKind != JsonValueKind.Object ||
                        !outbound.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(type.GetString()) ||
                        !outbound.TryGetProperty("server", out var host) || host.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(host.GetString()) ||
                        !outbound.TryGetProperty("server_port", out var port) || port.ValueKind != JsonValueKind.Number ||
                        !port.TryGetUInt16(out var number) || number == 0 || outbound.TryGetProperty("detour", out _))
                        throw new FormatException(i18N.Translate("Enter a standalone outbound with type, server and server_port."));

                    server.Outbound = outbound.Clone();
                    server.Hostname = host.GetString()!;
                    server.Port = number;
                    if (string.IsNullOrWhiteSpace(server.Remark))
                        server.Remark = fileName ?? server.Hostname;
                    form.DialogResult = DialogResult.OK;
                }
                catch (JsonException)
                {
                    MessageBox.Show(form, i18N.Translate("Invalid JSON."), form.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (FormatException ex)
                {
                    MessageBox.Show(form, ex.Message, form.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            return form.ShowDialog(Global.MainForm) == DialogResult.OK;
        }
    }

    public class SingBoxController : Guard, IServerController
    {
        public SingBoxController() : base("sing-box.exe") { }

        public override string Name => "sing-box";
        public ushort? Socks5LocalPort { get; set; }
        public string? LocalAddress { get; set; }
        protected override IEnumerable<string> StartedKeywords => new[] { "sing-box started" };

        public async Task<Socks5LocalServer> StartAsync(Server server)
        {
            var outbound = server is SingBoxServer singBox
                ? singBox.Outbound
                : JsonSerializer.SerializeToElement(new
                {
                    type = "shadowsocks",
                    server = server.Hostname,
                    server_port = server.Port,
                    method = ((ShadowsocksServer)server).EncryptMethod,
                    password = ((ShadowsocksServer)server).Password,
                    plugin = ((ShadowsocksServer)server).Plugin,
                    plugin_opts = ((ShadowsocksServer)server).PluginOption
                }, Global.NewCustomJsonSerializerOptions());

            var config = new
            {
                log = new { level = "info", disabled = false },
                inbounds = new[] { new { type = "socks", listen = this.LocalAddress(), listen_port = this.Socks5LocalPort() } },
                outbounds = new[] { outbound }
            };

            await using (var stream = File.Create(Constants.TempConfig))
                await JsonSerializer.SerializeAsync(stream, config);

            await StartGuardAsync("run -c ..\\data\\last.json");
            return new Socks5LocalServer(IPAddress.Loopback.ToString(), this.Socks5LocalPort(), server.Hostname);
        }
    }
}
