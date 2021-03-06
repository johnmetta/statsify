﻿using System;
using System.Linq;
using System.Net;

namespace Statsify.Client
{
    internal class HttpStatsifyChannel : IStatsifyChannel
    {
        private readonly Uri uri;

        public HttpStatsifyChannel(Uri uri)
        {
            this.uri = GetPostUri(uri);
        }

        public bool SupportsBatchedWrites
        {
            get { return true; }
        }

        public void WriteBuffer(byte[] buffer)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Method = "POST";
            httpWebRequest.MediaType = "application/vnd.statsify.datagram-v1+binary";

            using(var requestStream = httpWebRequest.GetRequestStream())
                requestStream.Write(buffer, 0, buffer.Length);

            try
            {
                using(httpWebRequest.GetResponse()) { } // using
            } // try
            catch
            {
                // FIXME: Requires sane exception handling
            } // catch
        }

        public void Dispose()
        {
        }

        internal static Uri GetPostUri(Uri uri)
        {
            var fragments = new[]{ "api", "v1", "metrics" };
            var lastFragment = 
                uri.
                    GetComponents(UriComponents.Path, UriFormat.Unescaped).
                    Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).
                    LastOrDefault();

            var uriBuilder = new UriBuilder(uri);
            if(string.IsNullOrWhiteSpace(lastFragment))
                uriBuilder.Path = string.Join("/", fragments);
            else
            {
                var lastFragmentIndex = Array.IndexOf(fragments, lastFragment.ToLowerInvariant());
                uriBuilder.Path =
                    uriBuilder.Path.TrimEnd('/') + "/" + string.Join("/", fragments.Skip(lastFragmentIndex + 1));
            } // else

            uri = uriBuilder.Uri;
            return uri;
        }
    }
}