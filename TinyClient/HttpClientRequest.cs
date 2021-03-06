﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using TinyClient.Helpers;
using TinyClient.Response;

namespace TinyClient
{
    public class HttpClientRequest
    {
        #region statics
        public static HttpClientRequest Create(HttpMethod method, string subQuery = "") 
            => new HttpClientRequest(method, subQuery);

        public static HttpClientRequest CreateGet(string subQuery) 
            => Create(HttpMethod.Get, subQuery);
        public static HttpClientRequest CreateGet()
            => CreateGet( "");

        public static HttpClientRequest CreatePost(string subQuery)
            => Create(HttpMethod.Post, subQuery);
        public static HttpClientRequest CreateJsonPost(string subQuery, object content) 
            => CreatePost(subQuery).SetContent(new JsonContent(content));
        public static HttpClientRequest CreateJsonPost(object content) => CreateJsonPost("", content);


        public static HttpClientRequest CreatePut(string subQuery)
            => Create(HttpMethod.Put, subQuery);
        public static HttpClientRequest CreateJsonPut(string subQuery, object content)
            => CreatePut(subQuery).SetContent(new JsonContent(content));
        public static HttpClientRequest CreateJsonPut(object content) => CreateJsonPut("", content);

        public static HttpClientRequest CreateDelete(string subQuery)
            => Create(HttpMethod.Delete, subQuery);
        public static HttpClientRequest CreateJsonDelete(string subQuery, object content)
            => CreateDelete(subQuery).SetContent(new JsonContent(content));
        public static HttpClientRequest CreateJsonDelete(object content) => CreateJsonDelete("", content);

        #endregion
        
        private readonly Dictionary<string, string> headers = new Dictionary<string, string>();
        private readonly Dictionary<string, string> queryParams = new Dictionary<string, string>();

        public HttpClientRequest(HttpMethod method, string subQuery)
        {
            Method = method;
            this.Query = subQuery;
            Content = null;
            Deserializer = new AutoResponseDeserializer();
        }
        public HttpMethod Method { get; }
        public IContent Content { get; private set; }
        public IResponseDeserializer Deserializer { get; private set; }
        public KeepAliveMode KeepAlive { get; private set; } = KeepAliveMode.UpToClient;
        public string Query { get; private set; }
        public KeyValuePair<string, string>[] CustomHeaders => headers.ToArray();
        public TimeSpan? Timeout { get; private set; }
        public IContentEncoder Encoder { get; private set; }
        public IContentEncoder Decoder { get; private set; }

        /// <summary>
        /// Full query path, includes Query and UriParams (without host)
        /// Starts with '/'
        /// 
        /// Example: "/search?text=hi"
        /// </summary>
        public string QueryAbsolutePath => UriHelper.GetQuery(Query, queryParams);

        public Uri GetUriFor(string host) => UriHelper.BuildUri(host, Query, queryParams);

        /// <summary>
        /// Adds or replace custom request header.
        /// </summary>
        public HttpClientRequest AddCustomHeader(string key, string value)
        {
            headers[key] = value;
            return this;
        }

        public HttpClientRequest AddAcceptEncoding(IContentEncoder decoder)
        {
            if(Decoder!=null)
                throw new InvalidOperationException("You can set decoder only once");
            Decoder = decoder;
            return this;

        }
        public HttpClientRequest AddContentEncoder(IContentEncoder encoder)
        {
            if(Encoder!=null)
                throw new InvalidOperationException("You can set encoder only once");
            Encoder = encoder;
            return this;
        }
        /// <summary>
        /// Adds custom request header.
        /// </summary>
        /// <exception cref="ArgumentException">header is already exist</exception>
        public HttpClientRequest AddCustomHeaderOrThrow(string key, string value)
        {
            headers.Add(key,value);
            return this;
        }

        public HttpClientRequest SetKeepAlive(bool keepAlive)
        {
            KeepAlive = keepAlive ? KeepAliveMode.True : KeepAliveMode.False;
            return this;
        }

        public HttpClientRequest SetDeserializer(IResponseDeserializer specificDeserializer) {
            this.Deserializer = specificDeserializer;
            return this;
        }
        
        public HttpClientRequest SetContent(IContent content) {
            this.Content = content;
            return this;
        }

        public HttpClientRequest SetTimeout(TimeSpan timeout) {
            this.Timeout = timeout;
            return this;
        }

        public HttpClientRequest AddUriParam(string paramName, object paramValue)
        {
            if (string.IsNullOrEmpty(paramName))
                throw new ArgumentNullException(nameof(paramName));
            if (paramValue == null)
                throw new ArgumentNullException(nameof(paramValue));

            queryParams[paramName] = UriHelper.SerializeUriParam(paramValue);
            return this;
        }

        /// <exception cref="InvalidDataException"/>
        public byte[] GetData(Uri host)
        {
            if(Content==null)
                return new byte[0];
            try
            {
                var memoryStream = new MemoryStream();
                if (Encoder != null)
                {
                    using (var encodingStream  = Encoder.GetEncodingStream(memoryStream))
                    {
                        Content.WriteTo(encodingStream, host);
                    }
                }
                else
                {
                    Content.WriteTo(memoryStream, host);
                }
                return memoryStream.ToArray();
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Request serialization error.", e);
            }
        }

        public HttpClientRequest GetCopyFor(string subQuery)
        {
            var copy = new HttpClientRequest(this.Method, subQuery)
            {
                Content = this.Content,
                Deserializer = this.Deserializer,
                Encoder = this.Encoder,
                KeepAlive = this.KeepAlive,
                Timeout = this.Timeout,
            };
            foreach (var header in headers)
            {
                copy.AddCustomHeader(header.Key, header.Value);
            }

            foreach (var queryParam in queryParams)
            {
                copy.AddUriParam(queryParam.Key, queryParam.Value);
            }

            return copy;
        }
        
    }

    public enum KeepAliveMode
    {
        True,
        False,
        UpToClient
    }
}
