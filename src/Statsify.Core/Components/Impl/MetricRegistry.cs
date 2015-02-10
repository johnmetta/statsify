﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Statsify.Core.Model;
using Statsify.Core.Storage;

namespace Statsify.Core.Components.Impl
{
    public class MetricRegistry : IMetricRegistry
    {
        private readonly string rootDirectory;

        public MetricRegistry(string rootDirectory)
        {
            this.rootDirectory = rootDirectory;
        }

        public IEnumerable<string> ResolveMetricNames(string metricNameSelector)
        {
            return GetDatabaseFiles(metricNameSelector).
                Select(f => {
                    var directoryName = Path.GetDirectoryName(f.FullName);
                    Debug.Assert(directoryName != null, "directoryName != null");
                    directoryName = directoryName.Substring(rootDirectory.Length + 1);
                    
                    var fileName = Path.GetFileNameWithoutExtension(f.FullName);
                    
                    return Path.Combine(directoryName, fileName).Replace(Path.DirectorySeparatorChar, '.');
                });
        }

        public Metric ReadMetric(string metricName, DateTime @from, DateTime until, TimeSpan? precision = null)
        {
            var databaseFilePath = GetDatabaseFiles(metricName).FirstOrDefault();
            if(databaseFilePath == null) return null;

            var database = DatapointDatabase.Open(databaseFilePath.FullName);
            var series = database.ReadSeries(from, until, precision);

            return new Metric(metricName, series);
        }

        private IEnumerable<FileInfo> GetDatabaseFiles(string metricNameSelector)
        {
            var fragments = metricNameSelector.Split('.');
            return GetDatabaseFiles(new DirectoryInfo(rootDirectory), fragments, 0);
        }

        private IEnumerable<FileInfo> GetDatabaseFiles(DirectoryInfo directoryInfo, string[] fragments, int i)
        {
            var fragment = fragments[i];

            if(i == fragments.Length - 1)
            {
                if(fragment.StartsWith("{") && fragment.EndsWith("}"))
                {
                    fragment = fragment.TrimStart('{').TrimEnd('}');
                    
                    var subfragments = fragment.Split(',');
                    foreach(var file in subfragments.SelectMany(subfragment => GetDatabaseFiles(directoryInfo, subfragment)))
                        yield return file;
                }
                else
                {
                    foreach(var file in GetDatabaseFiles(directoryInfo, fragment))
                        yield return file;
                } // else
            } // if
            else
            {
                if(fragment.StartsWith("{") && fragment.EndsWith("}"))
                {
                    fragment = fragment.TrimStart('{').TrimEnd('}');
                    
                    var subfragments = fragment.Split(',');
                    foreach(var file in subfragments.SelectMany(subfragment => GetDatabaseFiles(directoryInfo, subfragment, fragments, i)))
                        yield return file;
                } // if
                else
                {
                    foreach(var file in GetDatabaseFiles(directoryInfo, fragment, fragments, i))
                        yield return file;
                } // else
            } // else
        }

        private IEnumerable<FileInfo> GetDatabaseFiles(DirectoryInfo directoryInfo, string searchPattern)
        {
            var files = directoryInfo.GetFiles(searchPattern + ".db");
            foreach(var file in files)
                yield return file;
        } 

        private IEnumerable<FileInfo> GetDatabaseFiles(DirectoryInfo directoryInfo, string searchPatten, string[] fragments, int i)
        {
            foreach(var subdirectory in directoryInfo.GetDirectories(searchPatten))
            {
                var subdirectoryInfo = new DirectoryInfo(Path.Combine(directoryInfo.FullName, subdirectory.Name));
                foreach(var metricName in GetDatabaseFiles(subdirectoryInfo, fragments, i + 1))
                    yield return metricName;
            } // foreach
        }
    }
}