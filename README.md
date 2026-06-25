# HttpKit

A small, reusable Unity HTTP client built on `UnityWebRequest` + [UniTask](https://github.com/Cysharp/UniTask).
Zero game-specific dependencies — install it in any Unity project.

- **Rich result, never swallows errors** — `HttpResult<T>` distinguishes *success / empty (204) / network / timeout / server / deserialization*, instead of returning `null`.
- **No type-specific parsing in the transport** — per-call `deserialize` funcs + a pluggable `IJsonSerializer`.
- **Cross-cutting concerns via interceptors** — auth / 403 handling lives in `IResponseInterceptor` the app registers, not in the request layer.
- **Testable** — `UnityWebRequest` is isolated behind `IHttpTransport`; inject a fake in tests, no Play Mode needed.

## Requirements

| Dependency | How it resolves |
|---|---|
| Unity 2021.3+ | — |
| **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`) | Auto-resolved — declared in `package.json` (Unity registry). Only used by the default `NewtonsoftJsonSerializer`; swap `IJsonSerializer` to drop it. |
| **UniTask** (`com.cysharp.unitask`) | **Must be installed separately.** UniTask is distributed via Git/OpenUPM, not the Unity registry, so UPM can't auto-resolve it. See below. |

### Installing UniTask (required, do this first)
Add to your project's `Packages/manifest.json`:
```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
```

## Install

### Option A — Package Manager UI
`Window ▸ Package Manager ▸ + ▸ Add package from git URL…`
```
https://github.com/thinhtranbmt/httpkit-unity.git
```

### Option B — manifest.json
```json
"com.mycore.httpkit": "https://github.com/thinhtranbmt/httpkit-unity.git"
```

Pin a version with a tag: `…/HttpKit.git#v0.1.0`.

## Usage

```csharp
using HttpKit;

var http = new HttpKitClient(config: new HttpConfig { TimeoutSeconds = 15, Retries = 2 });

// typed GET
HttpResult<MyDto> res = await http.GetAsync<MyDto>(url, headers);
if (res.IsSuccess) Use(res.Data);
else Debug.LogError($"{res.Error}: {res.ErrorMessage}");   // Network / Timeout / Server / Deserialization

// POST with a body (serialized via the configured IJsonSerializer)
var r2 = await http.PostAsync<LoginResponse>(loginUrl, payloadObj, headers);
```

### Custom envelope without touching the transport
The transport never type-switches. When a caller needs a non-standard body shape, pass a `deserialize` func:
```csharp
// flatten { "TableName": {...} } -> Dictionary<tableName, json> (see KeyValueEnvelope)
var res = await http.GetAsync(url, headers, deserialize: KeyValueEnvelope.Flatten);
```

### Cross-cutting concerns (auth, 403, logging)
```csharp
public sealed class MyAuthInterceptor : IResponseInterceptor
{
    public void OnResponse(HttpRawResponse r)
    {
        if (r.StatusCode == 403) { /* re-auth, raise event, etc. */ }
    }
}

http.AddInterceptor(new MyAuthInterceptor());   // observes every raw response
```

### Custom serializer / transport
```csharp
var http = new HttpKitClient(
    transport: new MyFakeTransport(),    // IHttpTransport — e.g. canned responses in tests
    json:      new MyJsonSerializer());  // IJsonSerializer — e.g. System.Text.Json
```

## API surface

| File | Type(s) |
|---|---|
| `HttpKit.Core.cs` | `HttpVerb`, `HttpErrorKind`, `HttpRequest`, `HttpRawResponse`, `HttpResult<T>`, `IHttpConfig`/`HttpConfig`, `IJsonSerializer`, `IHttpTransport`, `IResponseInterceptor` |
| `HttpKitClient.cs` | `HttpKitClient` — `GetAsync`/`PostAsync`/`PutAsync`/`DeleteAsync`, retry, interceptors |
| `UnityWebRequestTransport.cs` | The only `UnityWebRequest`-aware file (swappable via `IHttpTransport`) |
| `NewtonsoftJsonSerializer.cs` | Default serializer (IL2CPP-safe non-generic path) |
| `KeyValueEnvelope.cs` | **Optional** helper for flat `{ key: value }` response bodies (`IKeyValueEnvelope` + `Flatten`/`Deserialize`) |

## Samples
Import **Roxane Adapter (template)** from the Package Manager (`Samples` tab) for an app-side
integration example — a shared client, a 403 interceptor, and a key-value envelope adapter.

## License
See [LICENSE.md](LICENSE.md).
