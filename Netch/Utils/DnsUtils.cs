using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog;

namespace Netch.Utils
{
    public static class DnsUtils
    {
        /// <summary>
        ///     缓存
        /// </summary>
        private static readonly ConcurrentDictionary<string, IPAddress> Cache = new();
        private static readonly ConcurrentDictionary<string, IPAddress> Cache6 = new();

        public static async Task<IPAddress?> LookupAsync(string hostname, AddressFamily inet = AddressFamily.Unspecified, int timeout = 3000)
        {
            try
            {
                var cacheResult = inet switch
                {
                    AddressFamily.Unspecified => Cache.GetValueOrDefault(hostname) ?? Cache6.GetValueOrDefault(hostname),
                    AddressFamily.InterNetwork => Cache.GetValueOrDefault(hostname),
                    AddressFamily.InterNetworkV6 => Cache6.GetValueOrDefault(hostname),
                    _ => throw new ArgumentOutOfRangeException()
                };

                if (cacheResult != null)
                    return cacheResult;

                return await LookupNoCacheAsync(hostname, inet, timeout);
            }
            catch (Exception e)
            {
                Log.Verbose(e, "Lookup hostname {Hostname} failed", hostname);
                return null;
            }
        }

        private static async Task<IPAddress?> LookupNoCacheAsync(string hostname, AddressFamily inet = AddressFamily.Unspecified, int timeout = 3000)
        {
            var task = Dns.GetHostAddressesAsync(hostname);
            var resTask = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);

            if (resTask == task)
            {
                var addresses = await task;

                var result = addresses.FirstOrDefault(i => inet == AddressFamily.Unspecified || inet == i.AddressFamily);
                if (result == null)
                    return null;

                switch (result.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        Cache.TryAdd(hostname, result);
                        break;
                    case AddressFamily.InterNetworkV6:
                        Cache6.TryAdd(hostname, result);
                        break;
                    default:
                        Trace.Assert(false);
                        break;
                }

                return result;
            }

            return null;
        }

        /// <summary>
        ///     查询
        /// </summary>
        /// <param name="hostname">主机名</param>
        /// <returns></returns>
        public static void ClearCache()
        {
            Cache.Clear();
            Cache6.Clear();
        }

        public static IEnumerable<string> Split(string dns)
        {
            return dns.SplitRemoveEmptyEntriesAndTrimEntries(',');
        }

        public static bool TrySplit(string value, out IEnumerable<string> result, ushort maxCount = 0)
        {
            result = Split(value).ToArray();

            return maxCount == 0 || result.Count() <= maxCount && result.All(ip => IPAddress.TryParse(ip, out _));
        }

        public static string Join(IEnumerable<string> dns)
        {
            return string.Join(",", dns);
        }
    }
}