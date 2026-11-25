using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netch.Models;

namespace UnitTest
{
    [TestClass]
    public class ServerDelayTests
    {
        [TestMethod]
        public async Task MedianIgnoresTheFastestAndSlowestSamplesAsync()
        {
            var result = await MeasureAsync(10, 180, 42);
            Assert.AreEqual(42, result);
        }

        [TestMethod]
        public async Task FailedSamplesAreNeverReportedAsMeasuredLatencyAsync()
        {
            Assert.AreEqual(40, await MeasureAsync(null, 30, 50));
            Assert.IsNull(await MeasureAsync(null, 30, null));
        }

        private static Task<int?> MeasureAsync(params int?[] samples)
        {
            var queue = new Queue<int?>(samples);
            return Server.MeasureDelayAsync(() => Task.FromResult(queue.Dequeue()));
        }
    }
}
