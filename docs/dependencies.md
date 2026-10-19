# Runtime setup and third-party licenses

The public Nashi packages contain Nashi, the platform runtime where needed, and the open-source libraries listed in their notices. Large media/Office executables are downloaded **directly from upstream**, after the user chooses setup. They are not relicensed under Nashi’s MIT license. The pinned hashes in source are checked before a tool directory is published. A failed or cancelled setup keeps existing tools and removes its own staging files. No GitHub token or browser cookies are used.

## macOS

Choose **Set up tools** in Home. Media tools enable video/audio conversion, media previews, less common image encoders and web downloads. Office tools enable Office conversions. Native image/PDF features can be used without those downloads. A previously installed `/Applications/LibreOffice.app` can also be used.

- [FFmpeg / ffprobe 9.0 Apple Silicon, OSXExperts](https://www.osxexperts.net/), GPL 2.0 or later. Downloaded executables are matched against the upstream published SHA-256. [FFmpeg sources](https://github.com/FFmpeg/FFmpeg). This is an end-user acquisition, not a redistribution of the upstream build by Nashi.
- [LibreOffice 26.8.1, The Document Foundation](https://www.libreoffice.org/download/), MPL 2.0 and included third-party licenses. Official 285 MB DMG; SHA-256 pinned from its official mirror listing. The publisher’s app signature is retained. [Sources](https://www.libreoffice.org/about-us/source-code/).
- [yt-dlp 2026.08.19](https://github.com/yt-dlp/yt-dlp/releases/tag/2026.08.19), GPLv3+ standalone combination. [Sources and complete third-party licenses](https://github.com/yt-dlp/yt-dlp/tree/2026.08.19).
- [Deno 2.9.7](https://github.com/denoland/deno/releases/tag/v2.9.7), MIT and included dependency licenses. [Sources](https://github.com/denoland/deno/tree/v2.9.7).

Exact URLs, limits and hashes: [`ExternalToolSetup.swift`](../Sources/NashiCore/ExternalToolSetup.swift). Other bundled library notices: [`Resources/THIRD-PARTY-NOTICES.txt`](../Resources/THIRD-PARTY-NOTICES.txt). ExifTool includes its complete Perl source; Sparkle includes its full license.

## Windows x64 preview

Choose **Set up media tools** once. Image, PDF and ZIP features work immediately. Setup uses these pinned upstream releases:

- [FFmpeg 9.0.2 full build, gyan.dev](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2), GPL. gyan.dev is listed by [FFmpeg’s official download page](https://ffmpeg.org/download.html). [Build information and sources](https://www.gyan.dev/ffmpeg/builds/).
- The same yt-dlp and Deno releases above, using their Windows x64 assets.

Exact URLs and hashes: [`Toolchain.cs`](../windows/Nashi.Core/Toolchain.cs). [Windows availability and shortcuts](../windows/README.md). [Bundled Windows library notices](../windows/THIRD-PARTY-NOTICES.txt). The preview is self-contained and does not require .NET to be installed separately.

We do not change operating-system security settings or request administrator access for this setup. OS publisher trust and Nashi’s pinned update-signature verification are separate: the first public builds are not Apple-notarized or Windows Authenticode signed.
