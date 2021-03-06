﻿using System;
using System.Net;
using System.Threading;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Hosting.Self;
using NLog;
using Statsify.Aggregator.Configuration;
using Statsify.Aggregator.Datagrams;
using Statsify.Aggregator.Http;
using Statsify.Aggregator.Network;
using Topshelf;
using Topshelf.Runtime;

namespace Statsify.Aggregator
{
    public class StatsifyAggregatorService : ServiceControl
    {
        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly StatsifyAggregatorConfigurationSection configuration;
        private readonly MetricAggregator metricAggregator;
        private readonly AnnotationAggregator annotationAggregator;
        private readonly ManualResetEvent stopEvent;
        private readonly DatagramParser datagramParser;
        private readonly Uri uri;
        private UdpDatagramReader udpDatagramReader;
        private Timer publisherTimer;
        private NancyHost nancyHost;

        public StatsifyAggregatorService(HostSettings hostSettings, ConfigurationManager configurationManager)
        {
            stopEvent = new ManualResetEvent(false);
            configuration = configurationManager.Configuration;
            var datapointDatabaseResolver = new DatapointDatabaseResolverCachingWrapper(new DatapointDatabaseResolver(configuration));
            metricAggregator = new MetricAggregator(configuration, datapointDatabaseResolver, stopEvent);
            annotationAggregator = new AnnotationAggregator(configuration);
            
            datagramParser = new DatagramParser(new MetricParser());

            var relativeUrl = configuration.HttpEndpoint.RelativeUrl;
            if(!relativeUrl.EndsWith("/"))
                relativeUrl += "/";

            var uriBuilder = new UriBuilder("http", configuration.HttpEndpoint.Address, configuration.HttpEndpoint.Port, relativeUrl);
            uri = uriBuilder.Uri;

            log.Info("initializing with Http Endpoint Url '{0}'", uri);
        }

        public bool Start(HostControl hostControl)
        {
            log.Info("starting");

            stopEvent.Reset();

            var ipAddress = IPAddress.Parse(configuration.UdpEndpoint.Address);

            var udpReceiveBufferSize = 
                int.Parse(System.Configuration.ConfigurationManager.AppSettings["udpReceiveBufferSize"]);

            udpDatagramReader = new UdpDatagramReader(udpReceiveBufferSize, ipAddress, configuration.UdpEndpoint.Port);
            udpDatagramReader.DatagramHandler += UdpDatagramReaderHandler;                        

            publisherTimer = new Timer(PublisherTimerCallback, null, configuration.Storage.FlushInterval, configuration.Storage.FlushInterval);                       

            NancyBootstrapperLocator.Bootstrapper = new Bootstrapper(/*configuration*/) { MetricAggregator = metricAggregator };
            StaticConfiguration.DisableRequestStreamSwitching = true;

            var hostConfiguration = new HostConfiguration();
            hostConfiguration.UrlReservations.CreateAutomatically = true;
            
            log.Info("starting NancyHost");

            nancyHost = new NancyHost(hostConfiguration, uri);
            nancyHost.Start();

            log.Info("started NancyHost");

            log.Info("starting");

            return true;
        }

        private void UdpDatagramReaderHandler(object sender, UdpDatagramEventArgs args)
        {
            var datagram = datagramParser.ParseDatagram(args.Buffer);
            AggregateDatagram(datagram);
        }

        private void AggregateDatagram(Datagram datagram)
        {
            if(datagram is AnnotationDatagram)
            {
                var annotationDatagram = datagram as AnnotationDatagram;
                annotationAggregator.Aggregate(annotationDatagram.Title, annotationDatagram.Message);
            } // if
            else if(datagram is MetricDatagram)
            {
                var metricDatagram = datagram as MetricDatagram;
                
                foreach(var metric in metricDatagram.Metrics)
                    metricAggregator.Aggregate(metric);
            } // else if
        }

        public bool Stop(HostControl hostControl)
        {
            log.Info("stopping");

            stopEvent.Set();

            if(publisherTimer != null)
                publisherTimer.Dispose();

            if(udpDatagramReader != null)
                udpDatagramReader.Dispose();

            if(nancyHost != null)
            {
                log.Info("stopping NancyHost");
                
                nancyHost.Dispose();
                
                log.Info("stopped NancyHost");
            } // if
            
            log.Info("stopped");

            return true;
        }

        private void PublisherTimerCallback(object state)
        {
            metricAggregator.Flush();            
        }
    }
}