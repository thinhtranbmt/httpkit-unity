using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HttpKit
{
    /// <summary>
    /// The facade. Compose it once and reuse:
    ///   var http = new HttpKitClient(config: new HttpConfig { TimeoutSeconds = 15 });
    ///   var res  = await http.GetAsync&lt;MyDto&gt;(url, headers);
    ///   if (res.IsSuccess) use(res.Data);
    ///
    /// Per-call `deserialize` overrides the default serializer — this is the seam that
    /// replaces the old hard-coded typeof(T) parsing in WebRequest.cs. A caller that needs
    /// a custom envelope (e.g. flatten { tableName: value } -&gt; dictionary) passes a func;
    /// the transport stays generic.
    /// </summary>
    public sealed class HttpKitClient
    {
        private readonly IHttpTransport _transport;
        private readonly IJsonSerializer _json;
        private readonly IHttpConfig _config;
        private readonly List<IResponseInterceptor> _interceptors = new List<IResponseInterceptor>();

        public HttpKitClient(IHttpTransport transport = null, IJsonSerializer json = null, IHttpConfig config = null)
        {
            _config = config ?? new HttpConfig();
            _transport = transport ?? new UnityWebRequestTransport(_config.VerboseLogging);
            _json = json ?? new NewtonsoftJsonSerializer();
        }

        public void AddInterceptor(IResponseInterceptor interceptor)
        {
            if (interceptor != null) _interceptors.Add(interceptor);
        }

        // -------------------- verbs --------------------

        public UniTask<HttpResult<T>> GetAsync<T>(
            string url, IDictionary<string, string> headers = null, Func<string, T> deserialize = null)
            => SendAsync(new HttpRequest { Verb = HttpVerb.Get, Url = url, Headers = headers }, deserialize);

        public UniTask<HttpResult<T>> PostAsync<T>(
            string url, object payload = null, IDictionary<string, string> headers = null, Func<string, T> deserialize = null)
            => SendAsync(new HttpRequest
            {
                Verb = HttpVerb.Post,
                Url = url,
                Headers = headers,
                Body = payload != null ? _json.Serialize(payload) : null,
                ContentType = "application/json"
            }, deserialize);

        public UniTask<HttpResult<T>> PutAsync<T>(
            string url, object payload, IDictionary<string, string> headers = null, Func<string, T> deserialize = null)
            => SendAsync(new HttpRequest
            {
                Verb = HttpVerb.Put,
                Url = url,
                Headers = headers,
                Body = payload != null ? _json.Serialize(payload) : null,
                ContentType = "application/json"
            }, deserialize);

        public UniTask<HttpResult<bool>> DeleteAsync(string url, IDictionary<string, string> headers = null)
            => SendAsync<bool>(new HttpRequest { Verb = HttpVerb.Delete, Url = url, Headers = headers }, _ => true);

        // -------------------- core --------------------

        public async UniTask<HttpResult<T>> SendAsync<T>(HttpRequest request, Func<string, T> deserialize = null)
        {
            HttpRawResponse raw = null;
            int attempt = 0;

            while (true)
            {
                raw = await _transport.SendAsync(request, _config.TimeoutSeconds);

                if (raw.IsNetworkError && attempt < _config.Retries)
                {
                    attempt++;
                    if (_config.VerboseLogging)
                        Debug.LogWarning($"[HttpKit] network error, retry {attempt}/{_config.Retries}: {raw.Error}");
                    continue;
                }

                break;
            }

            // Interceptors observe every response (success or error).
            for (int i = 0; i < _interceptors.Count; i++)
            {
                try { _interceptors[i].OnResponse(raw); }
                catch (Exception e) { Debug.LogError($"[HttpKit] interceptor threw: {e.Message}"); }
            }

            if (raw.IsNetworkError)
            {
                HttpErrorKind kind = raw.IsTimeout ? HttpErrorKind.Timeout : HttpErrorKind.Network;
                return HttpResult<T>.Fail(kind, raw.Error, raw.StatusCode, raw.Body, raw.Headers);
            }

            if (raw.IsServerError)
            {
                string msg = string.IsNullOrEmpty(raw.Body) ? raw.Error : raw.Body;
                return HttpResult<T>.Fail(HttpErrorKind.Server, msg, raw.StatusCode, raw.Body, raw.Headers);
            }

            // Success, but empty body → valid "no content" result.
            if (string.IsNullOrEmpty(raw.Body))
            {
                return HttpResult<T>.Ok(default, raw.StatusCode, raw.Body, raw.Headers);
            }

            try
            {
                T data = deserialize != null ? deserialize(raw.Body) : _json.Deserialize<T>(raw.Body);
                return HttpResult<T>.Ok(data, raw.StatusCode, raw.Body, raw.Headers);
            }
            catch (Exception e)
            {
                return HttpResult<T>.Fail(HttpErrorKind.Deserialization, e.Message, raw.StatusCode, raw.Body, raw.Headers);
            }
        }
    }
}
