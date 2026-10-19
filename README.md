<p align="center"><img src="Resources/Brand/NashiIcon.png" width="128" alt="Nashi"></p>
<h1 align="center">Nashi</h1>
<p align="center">Your files, a little sweeter. · Dosyaların için tatlı bir kolaylık.</p>

[English](docs/index.html) · [Türkçe](docs/tr/index.html) · [Releases](https://github.com/orkunisitmak/nashi-app/releases) · [Security](SECURITY.md)

Nashi is an independent, open-source file conversion and editing app inspired by
[Tangerine](https://tangerineformac.com/)'s drag-and-wheel interaction. It adds
public web-media downloads, QR reading, improved crop/annotation workflows,
keyboard media editing, live progress and signed automatic updates. Nashi is not
affiliated with Tangerine or LEAP STUDIO and includes no proprietary reference
app binaries or artwork.

## Project status

The macOS implementation is available here. Public release packaging and the
Windows port are being prepared; download links will be published in Releases
once their builds and security checks pass. Do not treat a CI artifact as a
maintainer-signed official release.

## Build macOS

Requires Apple Silicon, macOS 13+, and Xcode/Swift 6. Acquire the pinned FFmpeg,
ExifTool, yt-dlp and Deno tools with the scripts below. LibreOffice is required
for Office conversions; set `NASHI_LIBREOFFICE_SOURCE` to a local LibreOffice.app.

```sh
scripts/setup-tools.sh
scripts/setup-exiftool.sh
python3 scripts/setup-web-download-tools.py
swift test
scripts/build-app.sh release
```

The app is written to `build/Nashi.app`. Source builds use an ad-hoc signature
unless you explicitly provide your own signing profile. Signing private keys
are never distributed. macOS may warn about builds that have not been notarized.

## Privacy and updates

Conversion/editing run locally. Web downloads contact the selected media site.
Official releases check the public repository without a GitHub account or token.
Downloads and feeds are verified against a pinned signing key before updates
are installed. Work in progress finishes before an update can restart the app.

## License and acknowledgments

Nashi's original source is [MIT](LICENSE). Vendored libraries and external tools
retain their own licenses; see [third-party notices](Resources/THIRD-PARTY-NOTICES.txt).
No purchase flow or reference-app account is required.
