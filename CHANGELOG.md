# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/), and this project adheres to
[Semantic Versioning](https://semver.org/).

## [0.2.1] - 2026-08-31
### Fixed
- `samples[].path` in `package.json` still pointed at the pre-rename sample folder, so Package
  Manager listed a sample it could not import. Caught by the new packaging CI, which checks that
  every declared sample path exists.
- Sample `displayName` still carried the old game's name.

## [0.2.0] - 2026-08-31
### Fixed
- Declare `com.cysharp.unitask` in `package.json`. The runtime assembly referenced the
  `UniTask` assembly while the manifest declared nothing, so a fresh git-URL install left
  the reference unresolved. UniTask resolves from the OpenUPM scoped registry — see README.
- Commit Unity `.meta` files for every asset and folder. Without them Unity treats the
  package as an immutable folder with unimported assets and silently ignores all sources,
  so nothing compiled when the package was installed via UPM git URL.

### Changed
- `Samples~/RoxaneAdapter` renamed to `Samples~/BackendAdapter`; sample types renamed off the
  old game's name. `package.json` sample path and display name updated to match.

## [0.1.0] - 2026-06-25
### Added
- Initial release.
- `HttpKitClient` facade with `GetAsync`/`PostAsync`/`PutAsync`/`DeleteAsync`, retry on network error, and response interceptors.
- `HttpResult<T>` error model: `Network` / `Timeout` / `Server` / `Deserialization` / empty-204.
- Seams: `IHttpTransport` (default `UnityWebRequestTransport`), `IJsonSerializer` (default `NewtonsoftJsonSerializer`), `IResponseInterceptor`.
- Optional `KeyValueEnvelope` helper for flat `{ key: value }` response bodies.
- `Backend Adapter` sample (template).
