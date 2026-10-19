# Contributing

Please open an issue describing the problem, platform, app version and expected
behavior before proposing a large change. Small fixes can go directly to a PR.
Use synthetic, redistributable fixtures; never attach private documents or keys.

macOS: Xcode with Swift 6, macOS 13+, Apple Silicon. Run `swift test`.
Windows source and build instructions live under `windows/`.

New output formats and tools must preserve originals, choose unique output names,
clean partial exports on cancellation, and avoid shell interpolation. Test these
boundaries and actual output metadata rather than only checking file existence.

Nashi is independently developed and inspired by Tangerine's drag-and-wheel
interaction. Do not contribute proprietary Tangerine code, binaries, artwork,
screenshots or decompiled resources. Third-party licenses must be preserved.
