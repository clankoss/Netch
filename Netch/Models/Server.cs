using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Netch.Utils;

namespace Netch.Models
{
    public abstract class Server : ICloneable
    {
        /// <summary>
        ///     延迟
        /// </summary>
        [JsonIgnore]
        public int Delay { get; private set; } = -1;

        /// <summary>
        ///     组
        /// </summary>
        public string Group { get; set; } = Constants.DefaultGroup;

        /// <summary>
        ///     地址
        /// </summary>
        public string Hostname { get; set; } = string.Empty;

        /// <summary>
        ///     端口
        /// </summary>
        public ushort Port { get; set; }

        /// <summary>
        ///     倍率
        /// </summary>
        public double Rate { get; } = 1.0;

        /// <summary>
        ///     备注
        /// </summary>
        public string Remark { get; set; } = "";

        /// <summary>
        ///     代理类型
        /// </summary>
        public abstract string Type { get; }

        public object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        ///     获取备注
        /// </summary>
        /// <returns>备注</returns>
        public override string ToString()
        {
            var remark = string.IsNullOrWhiteSpace(Remark) ? $"{Hostname}:{Port}" : Remark;

            var shortName = ServerHelper.GetUtilByTypeName(Type).ShortName;

            return $"[{shortName}][{Group}] {remark}";
        }

        public abstract string MaskedData();

        /// <summary>
        ///     测试延迟
        /// </summary>
        /// <returns>延迟</returns>
        public async Task<int> PingAsync(CancellationToken cancellationToken = default)
        {
            var destination = await DnsUtils.LookupAsync(Hostname);
            if (destination == null)
                return Delay = -2;

            cancellationToken.ThrowIfCancellationRequested();
            var tcp = Global.Settings.ServerTCPing;
            var result = await MeasureDelayAsync(() => tcp
                ? Utils.Utils.TCPingAsync(destination, Port, ct: cancellationToken)
                : Utils.Utils.ICMPingAsync(destination), cancellationToken);

            return Delay = result ?? -3;
        }

        internal static async Task<int?> MeasureDelayAsync(Func<Task<int?>> probe, CancellationToken cancellationToken = default)
        {
            var samples = new List<int>(3);
            for (var i = 0; i < 3; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await probe() is int delay)
                    samples.Add(delay);
            }
            cancellationToken.ThrowIfCancellationRequested();

            samples.Sort();
            return samples.Count switch
            {
                2 => (samples[0] + samples[1]) / 2,
                3 => samples[1],
                _ => null
            };
        }
    }

    public static class ServerExtension
    {
        public static async Task<string> AutoResolveHostnameAsync(this Server server, AddressFamily inet = AddressFamily.Unspecified)
        {
            // ! MainController cached
            return (await DnsUtils.LookupAsync(server.Hostname, inet))!.ToString();
        }

    }
}