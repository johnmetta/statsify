﻿namespace Statsify.Client
{
    public class NullStatsifyClient : IStatsifyClient
    {
        public void Counter(string metric, double value, double sample = 1)
        {
        }

        public void Gauge(string metric, double value, double sample = 1)
        {
        }

        public void GaugeDiff(string metric, double value, double sample = 1)
        {
        }

        public void Time(string metric, double value, double sample = 1)
        {
        }

        public void Annotation(string title, string message)
        {
        }
    }
}