# WebGL optimization notes

This project is configured for a small WebGL production build.

## Unity build settings applied

- WebGL compression: Brotli
- Decompression fallback: disabled
- WebGL data caching: enabled
- WebGL file names as hashes: enabled
- WebGL build-size analysis: enabled
- Managed stripping level for WebGL: Medium
- Strip unused mesh components: enabled
- Unity Analytics package/module removed
- Player logging disabled
- Large music/stem audio imports: streaming, background load, Vorbis quality 0.55
- Large texture WebGL overrides: max 1024px, compressed, quality 40
- Large FBX imports: mesh compression enabled, imported cameras/lights disabled
- Addressables bundle cache enabled

## Hosting requirement

Because decompression fallback is disabled, the server must serve Brotli files with correct headers. If headers are wrong, the WebGL build can fail to load.

Required headers for compressed files:

- `.wasm.br`: `Content-Type: application/wasm`, `Content-Encoding: br`
- `.js.br`: `Content-Type: application/javascript`, `Content-Encoding: br`
- `.data.br`: `Content-Type: application/octet-stream`, `Content-Encoding: br`
- `.symbols.json.br`: `Content-Type: application/json`, `Content-Encoding: br`

If your host cannot set Brotli headers, switch Unity Player Settings > WebGL > Publishing Settings > Compression Format to Gzip or enable Decompression Fallback. That will make the build bigger.

## Bigger future optimization

For the smallest possible initial load, move `Main`, `Shop`, characters, themes, and large bundles to remote Addressables. That requires a public remote asset URL and uploading Addressables bundles separately from the WebGL loader.
