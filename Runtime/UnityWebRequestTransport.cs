using System;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HttpKit
{
    /// <summary>
    /// IHttpTransport backed by UnityWebRequest. The only Unity-networking-aware file in the kit.
    /// Never throws to the caller — failures are reported through HttpRawResponse flags.
    /// </summary>
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        private readonly bool _verbose;

        public UnityWebRequestTransport(bool verbose = false)
        {
            _verbose = verbose;
        }

        public UniTask<HttpRawResponse> SendAsync(HttpRequest request, int timeoutSeconds)
        {
            var tcs = new UniTaskCompletionSource<HttpRawResponse>();

            UnityWebRequest uwr;
            try
            {
                uwr = Build(request);
            }
            catch (Exception e)
            {
                tcs.TrySetResult(new HttpRawResponse { IsNetworkError = true, Error = e.Message });
                return tcs.Task;
            }

            if (timeoutSeconds > 0)
            {
                uwr.timeout = timeoutSeconds;
            }

            if (_verbose)
            {
                string headerDump = request.Headers != null
                    ? string.Join("\n", request.Headers.Select(h => h.Key + ": " + h.Value))
                    : string.Empty;
                Debug.Log($"[HttpKit] -> {request.Verb.ToString().ToUpper()} {request.Url}\n{headerDump}\n{request.Body ?? string.Empty}");
            }

            // Event-based completion works in both Play Mode and Edit Mode (no UniTask PlayerLoop needed).
            uwr.SendWebRequest().completed += _ =>
            {
                bool network = uwr.result == UnityWebRequest.Result.ConnectionError
                               && uwr.error != "Redirect limit exceeded";
                bool timeout = network && !string.IsNullOrEmpty(uwr.error)
                               && uwr.error.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0;
                bool server = uwr.responseCode >= 400;

                var resp = new HttpRawResponse
                {
                    StatusCode = uwr.responseCode,
                    Body = uwr.downloadHandler != null ? uwr.downloadHandler.text : null,
                    IsNetworkError = network,
                    IsTimeout = timeout,
                    IsServerError = server,
                    Error = uwr.error,
                    Headers = uwr.GetResponseHeaders()
                };

                if (_verbose)
                {
                    Debug.Log($"[HttpKit] <- {resp.StatusCode} {request.Url}\n{resp.Body}\n{resp.Error}");
                }

                uwr.Dispose();
                tcs.TrySetResult(resp);
            };

            return tcs.Task;
        }

        private static UnityWebRequest Build(HttpRequest r)
        {
            UnityWebRequest uwr;

            switch (r.Verb)
            {
                case HttpVerb.Get:
                    uwr = UnityWebRequest.Get(r.Url);
                    break;

                case HttpVerb.Delete:
                    uwr = UnityWebRequest.Delete(r.Url);
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    break;

                case HttpVerb.Post:
                    if (string.IsNullOrEmpty(r.Body))
                    {
#if UNITY_2022_2_OR_NEWER
                        uwr = UnityWebRequest.PostWwwForm(r.Url, string.Empty);
#else
                        uwr = UnityWebRequest.Post(r.Url, string.Empty);
#endif
                    }
                    else
                    {
                        uwr = new UnityWebRequest(r.Url, UnityWebRequest.kHttpVerbPOST)
                        {
                            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(r.Body)),
                            downloadHandler = new DownloadHandlerBuffer()
                        };
                    }
                    break;

                case HttpVerb.Put:
                    if (string.IsNullOrEmpty(r.Body))
                    {
                        throw new ArgumentException("PUT body cannot be empty.");
                    }
                    uwr = new UnityWebRequest(r.Url, UnityWebRequest.kHttpVerbPUT)
                    {
                        uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(r.Body)),
                        downloadHandler = new DownloadHandlerBuffer()
                    };
                    break;

                default:
                    throw new ArgumentException("Unknown verb " + r.Verb);
            }

            if (!string.IsNullOrEmpty(r.ContentType))
            {
                uwr.SetRequestHeader("Content-Type", r.ContentType);
            }

            if (r.Headers != null)
            {
                foreach (var h in r.Headers)
                {
                    uwr.SetRequestHeader(h.Key, h.Value);
                }
            }

            return uwr;
        }
    }
}
