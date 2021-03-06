﻿using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Statsify.Core.Model;
using Statsify.Core.Storage;

namespace Statsify.Tests.Core.Storage
{
    [TestFixture]
    public class DatapointDatabaseTests
    {
        [Test]
        public void CreateOpen()
        {
            var path = Path.GetTempFileName();

            DatapointDatabase.Create(path, 0.5f, DownsamplingMethod.Sum, 
                new RetentionPolicy {
                    { TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1) },
                    { TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5) },
                    { TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(20) }
                });

            var database = DatapointDatabase.Open(path);

            Assert.AreEqual(0.5f, database.DownsamplingFactor);
            Assert.AreEqual(DownsamplingMethod.Sum, database.DownsamplingMethod);

            Assert.AreEqual(3, database.Archives.Count);

            Assert.AreEqual((TimeSpan)database.Archives[0].Retention.Precision, TimeSpan.FromSeconds(1));
            Assert.AreEqual((TimeSpan)database.Archives[0].Retention.History, TimeSpan.FromMinutes(1));

            Assert.AreEqual((TimeSpan)database.Archives[1].Retention.Precision, TimeSpan.FromSeconds(10));
            Assert.AreEqual((TimeSpan)database.Archives[1].Retention.History, TimeSpan.FromMinutes(5));

            Assert.AreEqual((TimeSpan)database.Archives[2].Retention.Precision, TimeSpan.FromSeconds(20));
            Assert.AreEqual((TimeSpan)database.Archives[2].Retention.History, TimeSpan.FromMinutes(20));
        }

        [Test]
        public void Create()
        {
            var n = 0;
            var date = new DateTime(1971, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // ReSharper disable once AccessToModifiedClosure
            Func<DateTime> currentTimeProvider = () => date.AddSeconds(n);

            var database = 
                DatapointDatabase.Create(Path.GetTempFileName(), 0.5f, DownsamplingMethod.Last, 
                    new RetentionPolicy {
                        { TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1) },
                        { TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1) },
                        { TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(1) }
                    }, 
                    currentTimeProvider);

            for(n = 0; n < 120; ++n)
                database.WriteDatapoint(currentTimeProvider(), n);

            n -= 1;
            var now = currentTimeProvider();

            var series = database.ReadSeries(now.AddSeconds(-60), now);
            CollectionAssert.AreEqual(
                Enumerable.Range(60, 60).Select(v => (double?)v).ToArray(),
                series.Datapoints.Select(d => d.Value).ToArray());

            series = database.ReadSeries(now.AddSeconds(-60), now, TimeSpan.FromSeconds(10));
            CollectionAssert.AreEqual(
                new double?[] { 69, 79, 89, 99, 109, 119 }, 
                series.Datapoints.Select(d => d.Value).ToArray());

            series = database.ReadSeries(now.AddSeconds(-60), now, TimeSpan.FromSeconds(20));
            CollectionAssert.AreEqual(
                new double?[] { 79, 99, 119 },
                series.Datapoints.Select(d => d.Value).ToArray());
        }

        [Test]
        public void WriteDatapoints()
        {
            var n = 0;
            var date = new DateTime(1971, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Timestamp.DebuggerDisplayFormatter = t => ((DateTime)t).ToString("HH:mm:ss");

            // ReSharper disable once AccessToModifiedClosure
            Func<DateTime> currentTimeProvider = () => date.AddSeconds(n);

            var path = Path.GetTempFileName();
            var database = 
                DatapointDatabase.Create(path, 0, DownsamplingMethod.Average, 
                    new RetentionPolicy {
                        { TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1) },
                        { TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10) },
                        { TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(60) }
                    }, 
                    currentTimeProvider);

            database.WriteDatapoints(
                Enumerable.
                    Range(0, 3600).
                    Select(i =>new Datapoint(currentTimeProvider(), ++n)).
                    ToList());

            n--;
            var now = currentTimeProvider();

            var datapoints = database.ReadSeries(now.AddSeconds(-10), now, TimeSpan.FromSeconds(1)).Datapoints.Select(d => d.Value).OrderByDescending(v => v).ToArray();
            CollectionAssert.AreEqual(
                new double[] { 3600, 3599, 3598, 3597, 3596, 3595, 3594, 3593, 3592, 3591 },
                datapoints);

            datapoints = database.ReadSeries(now.AddMinutes(-1), now, TimeSpan.FromSeconds(10)).Datapoints.Select(d => d.Value).OrderByDescending(v => v).ToArray();
            CollectionAssert.AreEqual(
                new[] { 3595.5, 3585.5, 3575.5, 3565.5, 3555.5, 3545.5 },
                datapoints);

            datapoints = database.ReadSeries(now.AddMinutes(-1), now, TimeSpan.FromSeconds(20)).Datapoints.Select(d => d.Value).OrderByDescending(v => v).ToArray();
            CollectionAssert.AreEqual(
                new[] { 3590.5, 3570.5, 3550.5 },
                datapoints);
        }
    }
}
