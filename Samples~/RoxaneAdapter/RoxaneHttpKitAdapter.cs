// =====================================================================
// SAMPLE / TEMPLATE — NOT COMPILED (Samples~ folder).
//
// Shows how Roxane would adopt HttpKit and, in doing so, DELETE the two warts
// in the old NetworkSystem:
//   1) the hard-coded typeof(T) envelope parsing in WebRequest.cs
//   2) the InGameEvent 403 handling baked into the transport
//
// It also fixes the DataToolKit integration gap we found: the DataToolKit
// IDataToolNetwork adapter normalizes the { tableName: value } envelope itself,
// instead of depending on WebRequest's type-specific special-casing.
//
// References app-specific types (ServiceShared, InGameEvent, DataToolKit, ErrorResponse)
// that won't exist in a fresh project, so it is guarded by HTTPKIT_SAMPLES and stays
// inert by default. Read it as a reference; to compile, add HTTPKIT_SAMPLES to your
// Scripting Define Symbols and adapt the type names.
// =====================================================================
#if HTTPKIT_SAMPLES

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HttpKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Roxane.Net
{
    // ---------------------------------------------------------------
    // 1) The envelope flatten that used to be hard-coded in WebRequest is now the
    //    reusable HttpKit.KeyValueEnvelope helper. Server returns { "TableName": <object> };
    //    KeyValueEnvelope.Flatten -> tableName -> compact json string.
    //    (kept this alias only so older call sites compile; prefer KeyValueEnvelope.Flatten)
    // ---------------------------------------------------------------
    public static class DataToolEnvelope
    {
        public static Dictionary<string, string> Flatten(string json) => KeyValueEnvelope.Flatten(json);
    }

    // ---------------------------------------------------------------
    // 2) 403 "logged in on another device" — moved OUT of the transport into
    //    an interceptor the app registers. The HTTP layer no longer knows InGameEvent.
    // ---------------------------------------------------------------
    public sealed class LoginOtherDeviceInterceptor : IResponseInterceptor
    {
        public void OnResponse(HttpRawResponse r)
        {
            if (r.StatusCode != 403 || string.IsNullOrEmpty(r.Body))
            {
                return;
            }
            try
            {
                ErrorResponse err = JsonUtility.FromJson<ErrorResponse>(r.Body);
                if (err != null && err.code == "ALREADY_LOGIN_OTHER_PLAYERID")
                {
                    InGameEvent.OnDetectLoginAnotherDevice();
                }
            }
            catch { /* not the error shape we care about */ }
        }
    }

    // ---------------------------------------------------------------
    // A single shared client for the whole app.
    // ---------------------------------------------------------------
    public static class RoxaneHttp
    {
        public static readonly HttpKitClient Client = Build();

        private static HttpKitClient Build()
        {
            var client = new HttpKitClient(config: new HttpConfig
            {
                TimeoutSeconds = 15,
                Retries = 2,
                VerboseLogging = ServiceShared.CURRENT_ENV == ServiceShared.Environment.Dev
            });
            client.AddInterceptor(new LoginOtherDeviceInterceptor());
            return client;
        }

        public static Dictionary<string, string> AuthHeaders()
            => new Dictionary<string, string> { { "x-api-key", ServiceShared.GetAPIKey() } };
    }

    // ---------------------------------------------------------------
    // 3) DataToolKit adapter on top of HttpKit — fixes the integration gap.
    //    Returns a populated DataToolKit.DataToolResponseData WITHOUT relying on
    //    WebRequest's hard-coded normalization.
    // ---------------------------------------------------------------
    public sealed class HttpKitDataToolNetwork : DataToolKit.IDataToolNetwork
    {
        public async UniTask<T> GetAsync<T>(string url, IDictionary<string, string> headers)
        {
            // DataToolKit only ever asks for DataToolResponseData here.
            HttpResult<Dictionary<string, string>> res =
                await RoxaneHttp.Client.GetAsync(url, headers, deserialize: DataToolEnvelope.Flatten);

            if (!res.IsSuccess)
            {
                Debug.LogError($"[DataTool] {res.Error} {res.ErrorMessage} @ {url}");
                return (T)(object)new DataToolKit.DataToolResponseData();
            }

            return (T)(object)new DataToolKit.DataToolResponseData { Data = res.Data };
        }
    }

    // ---------------------------------------------------------------
    // Plain typed call example (replaces NetworkHandler.PostAsync<T>):
    //   var res = await RoxaneHttp.Client.PostAsync<LoginResponse>(
    //                 ServiceShared.GetLoginURL(), payload, RoxaneHttp.AuthHeaders());
    //   if (!res.IsSuccess) { handle res.Error; }
    //   else use(res.Data);
    // ---------------------------------------------------------------
}

#endif
