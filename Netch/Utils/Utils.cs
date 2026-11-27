using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaxMind.GeoIP2;
using Serilog;
using Task = System.Threading.Tasks.Task;

namespace Netch.Utils
{
    public static class Utils
    {
        public static void Open(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true
                });
            }
            catch (Exception e)
            {
                Log.Warning(e, "打开 {Uri} 失败", path);
            }
        }

        public static async Task<int?> TCPingAsync(IPAddress ip, int port, int timeout = 1000, CancellationToken ct = default)
        {
            using var client = new TcpClient(ip.AddressFamily);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(timeout);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await client.ConnectAsync(ip, port, deadline.Token);
                return Convert.ToInt32(stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (SocketException)
            {
                return null;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }

        public static async Task<int?> ICMPingAsync(IPAddress ip, int timeout = 1000)
        {
            using var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync(ip, timeout);
                return reply.Status == IPStatus.Success ? Convert.ToInt32(reply.RoundtripTime) : null;
            }
            catch (PingException)
            {
                return null;
            }
        }

        public static async Task<string> GetCityCodeAsync(string address)
        {
            var i = address.IndexOf(':');
            if (i != -1)
                address = address[..i];

            string? country = null;
            try
            {
                var databaseReader = new DatabaseReader("bin\\GeoLite2-Country.mmdb");

                if (IPAddress.TryParse(address, out _))
                {
                    country = databaseReader.Country(address).Country.IsoCode;
                }
                else
                {
                    var dnsResult = await DnsUtils.LookupAsync(address);

                    if (dnsResult != null)
                        country = databaseReader.Country(dnsResult).Country.IsoCode;
                }
            }
            catch
            {
                // ignored
            }

            country ??= "Unknown";

            return country;
        }

        public static string SHA256CheckSum(string filePath)
        {
            try
            {
                using var fileStream = File.OpenRead(filePath);
                return SHA256ComputeCore(fileStream);
            }
            catch (Exception e)
            {
                Log.Warning(e, $"Compute file \"{filePath}\" sha256 failed");
                return "";
            }
        }

        private static string SHA256ComputeCore(Stream stream)
        {
            using var sha256 = SHA256.Create();
            return string.Concat(sha256.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        public static string GetFileVersion(string file)
        {
            if (File.Exists(file))
                return FileVersionInfo.GetVersionInfo(file).FileVersion ?? "";

            return "";
        }

        public static void DrawCenterComboBox(object sender, DrawItemEventArgs e)
        {
            if (sender is ComboBox cbx)
            {
                e.DrawBackground();

                if (e.Index < 0)
                    return;

                TextRenderer.DrawText(e.Graphics,
                    cbx.Items[e.Index].ToString(),
                    cbx.Font,
                    e.Bounds,
                    (e.State & DrawItemState.Selected) == DrawItemState.Selected ? SystemColors.HighlightText : cbx.ForeColor,
                    TextFormatFlags.HorizontalCenter);
            }
        }

        public static void ComponentIterator(in Component component, in Action<Component> func)
        {
            func.Invoke(component);
            switch (component)
            {
                case ListView listView:
                    // ListView sub item
                    foreach (var item in listView.Columns.Cast<ColumnHeader>())
                        ComponentIterator(item, func);

                    break;
                case ToolStripMenuItem toolStripMenuItem:
                    // Iterator Menu strip sub item
                    foreach (var item in toolStripMenuItem.DropDownItems.Cast<ToolStripItem>())
                        ComponentIterator(item, func);

                    break;
                case MenuStrip menuStrip:
                    // Menu Strip
                    foreach (var item in menuStrip.Items.Cast<ToolStripItem>())
                        ComponentIterator(item, func);

                    break;
                case ContextMenuStrip contextMenuStrip:
                    foreach (var item in contextMenuStrip.Items.Cast<ToolStripItem>())
                        ComponentIterator(item, func);

                    break;
                case Control control:
                    foreach (var c in control.Controls.Cast<Control>())
                        ComponentIterator(c, func);

                    if (control.ContextMenuStrip != null)
                        ComponentIterator(control.ContextMenuStrip, func);

                    break;
            }
        }

        public static void ChangeControlForeColor(Component component, Color color)
        {
            switch (component)
            {
                case TextBox _:
                case ComboBox _:
                    if (((Control)component).ForeColor != color)
                        ((Control)component).ForeColor = color;

                    break;
            }
        }

        public static int SubnetToCidr(string value)
        {
            var subnet = IPAddress.Parse(value);
            return SubnetToCidr(subnet);
        }

        public static int SubnetToCidr(IPAddress subnet)
        {
            return subnet.GetAddressBytes().Sum(b => Convert.ToString(b, 2).Count(c => c == '1'));
        }

        public static string HostAppendPort(string host, ushort port = 53)
        {
            if (!host.Contains(':'))
                host += $":{port}";

            return host;
        }

        public static string GetHostFromUri(string str)
        {
            var startIndex = str.LastIndexOf('/');
            if (startIndex != -1)
                str = str[(startIndex + 1)..];

            var endIndex = str.IndexOf(':');
            return endIndex == -1 ? str : str[..endIndex];
        }
    }
}