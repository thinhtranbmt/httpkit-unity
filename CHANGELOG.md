# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/), and this project adheres to
[Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-06-25
### Added
- Initial release.
- `HttpKitClient` facade with `GetAsync`/`PostAsync`/`PutAsync`/`DeleteAsync`, retry on network error, and response interceptors.
- `HttpResult<T>` error model: `Network` / `Timeout` / `Server` / `Deserialization` / empty-204.
- Seams: `IHttpTransport` (default `UnityWebRequestTransport`), `IJsonSerializer` (default `NewtonsoftJsonSerializer`), `IResponseInterceptor`.
- Optional `KeyValueEnvelope` helper for flat `{ key: value }` response bodies.
- `Roxane Adapter` sample (template).
