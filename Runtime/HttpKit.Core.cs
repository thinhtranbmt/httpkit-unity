using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace HttpKit
{
    // =====================================================================
    // HttpKit — reusable Unity HTTP client. Zero game-specific dependencies.
    // Hard dependencies: UnityEngine + UniTask (+ Newtonsoft for the default serializer).
    //
    // Design goals (fixing the old NetworkSystem):
    //  - NO type-specific parsing baked into the transport. Per-call `deserialize`
    //    funcs / pluggable IJsonSerializer replace the hard-coded typeof(T) switch.
    //  - Rich HttpResult<T> instead of silently returning null on error.
    //  - Cross-cutting concerns (auth/403 handling) via IResponseInterceptor the APP
    //    registers — not wired into the request layer.
    //  - UnityWebRequest isolated behind IHttpTransport (swappable / testable).
    // =====================================================================

    public enum HttpVerb { Get, Post, Put, Delete }

    public enum HttpErrorKind { None, Network, Timeout, Server, Deserialization }

    /// <summary>A request description, independent of any transport.</summary>
    public sealed class HttpRequest
    {
        public HttpVerb Verb;
        public string Url;
        public IDictionary<string, string> Headers;
        public string Body;          // already-serialized payload, or null
        public string ContentType;   // e.g. "application/json"
    }

    /// <summary>Raw transport-level response, before typed deserialization.</summary>
    public sealed class HttpRawResponse
    {
        public long StatusCode;
        public string Body;
        public bool IsNetworkError;
        public bool IsTimeout;
        public bool IsServerError;   // status >= 400
        public string Error;
        public IDictionary<string, string> Headers;
    }

    /// <summary>Typed result. Never throws on the happy path; inspect IsSuccess/Error.</summary>
    public sealed class HttpResult<T>
    {
        public bool IsSuccess;
        public long StatusCode;
        public T Data;
        public string RawBody;
        public HttpErrorKind Error;
        public string ErrorMessage;
        public IDictionary<string, string> Headers;

        public static HttpResult<T> Ok(T data, long status, string rawBody, IDictionary<string, string> headers)
            => new HttpResult<T>
            {
                IsSuccess = true, Data = data, StatusCode = status,
                RawBody = rawBody, Headers = headers, Error = HttpErrorKind.None
            };

        public static HttpResult<T> Fail(HttpErrorKind kind, string message, long status, string rawBody, IDictionary<string, string> headers)
            => new HttpResult<T>
            {
                IsSuccess = false, Error = kind, ErrorMessage = message,
                StatusCode = status, RawBody = rawBody, Headers = headers
            };
    }

    public interface IHttpConfig
    {
        int TimeoutSeconds { get; }
        int Retries { get; }
        bool VerboseLogging { get; }
    }

    public sealed class HttpConfig : IHttpConfig
    {
        public int TimeoutSeconds { get; set; } = 15;
        public int Retries { get; set; } = 2;
        public bool VerboseLogging { get; set; } = false;
    }

    /// <summary>Serialization seam. Default impl uses Newtonsoft; swap for tests/other engines.</summary>
    public interface IJsonSerializer
    {
        T Deserialize<T>(string json);
        string Serialize(object value);
    }

    /// <summary>Transport seam. Unity impl provided; mock it in tests.</summary>
    public interface IHttpTransport
    {
        UniTask<HttpRawResponse> SendAsync(HttpRequest request, int timeoutSeconds);
    }

    /// <summary>
    /// Cross-cutting hook invoked for every raw response (success or error).
    /// E.g. detect a 403 "logged in on another device" — registered by the app, not the transport.
    /// </summary>
    public interface IResponseInterceptor
    {
        void OnResponse(HttpRawResponse response);
    }
}
