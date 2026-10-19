# Nashi for Windows (x64 preview)

Native WPF / .NET 10 port. Build with `dotnet publish windows/Nashi.Windows -c Release -o build/windows/Nashi`. The resulting folder is self-contained; users do not install .NET.

Implemented: image PNG/JPG/BMP/GIF/TIFF/PDF conversion, resize, crop (drag / Shift aspect lock / Enter), rotate, flip, annotation with separate marker and rectangle modes, JPEG compression and metadata-free exports; PDF merge, split and page ordering; safe ZIP creation/extraction; local media conversion and 15 FFmpeg tools; public-link video/audio/thumbnail downloads; a sound-enabled tools wheel; borderless progress and three-second success cards; signed anonymous update staging and restart.

Drop files onto Nashi or press Ctrl+O. Ctrl+Space opens the selected files’ wheel. Media editors use Space for play/pause, Left/Right for one-second seeking, I/O for range limits, Enter to save and Escape to cancel. Native Windows media preview depends on installed Windows codecs; export uses FFmpeg.

Choose **Set up media tools** once for a hash-verified direct download from GyanD/codexffmpeg, yt-dlp and Deno. No administrator password, GitHub token or browser credentials. Upstream packages are not bundled or relicensed under MIT. See THIRD-PARTY-NOTICES.txt and Nashi.Core/Toolchain.cs for exact release URLs and hashes.

Windows does not yet include the macOS global Finder gesture, AirDrop, Float, folder automation, Office/Pages conversion, QR reading or background removal. GIF import uses the first frame. The website’s 27-tool / 45-input catalog describes macOS; this Windows preview has its own availability list.

`Nashi.Verify` exercises the cross-platform engine, transactions, traversal protection and update trust. `Nashi --smoke-test` runs Windows image/PDF/ZIP and WPF rendering checks; set `NASHI_SMOKE_DIRECTORY` to collect its JSON and PNGs. These automated checks do not claim physical mouse or Explorer global gesture testing.
