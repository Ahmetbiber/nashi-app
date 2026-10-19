# Nashi showcase media

- `mac-home.png`, `mac-wheel.png`, `mac-crop.png`: genuine window captures from the public macOS 0.7.3 app, taken in an isolated demo session using Nashi's own brand artwork.
- `windows-home-preview.png`: native WPF UI capture from the tested development build at `0db59537dd4615fe37ed8153ad29d80de2a61587` ([PR #1](https://github.com/orkunisitmak/nashi-app/pull/1)). Empty black capture padding below the window was trimmed. This is ahead of the public Windows release.
- `nashi-demo.mp4`: a silent, 23.4-second guided tour assembled from actual Mac app captures, with editorial titles and fades. It is a sequence of captured states, rather than a continuous screen recording. The demonstrated PNG-to-JPG conversion and square crop were performed in the app and their output files verified.
- `nashi-demo.gif`: lightweight animated README preview of the same tour. The README links to the video player for playback controls and captions.
- `demo-poster.jpg`: video poster. `demo-en.vtt` / `demo-tr.vtt`: English and Turkish captions.

No personal files, account information or reference-app artwork appear in these assets. The app captures contain test filenames and Nashi artwork only. Image metadata was stripped when preparing the public copies.

The MP4 uses H.264, YUV420P and fast-start metadata. Both language pages use a native HTML video player with controls, captions and a text transcript; playback never starts automatically. Keep the `media-src 'self'` content-security policy when rebuilding the pages with `scripts/build-site.py`.
