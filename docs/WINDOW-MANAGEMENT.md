# Window management

Nashi can arrange a window while its native title bar or resize border is being dragged. Tap **Shift or Command on macOS**, or **Shift or Alt on Windows**, move over a wheel slice and release the mouse. Releasing the modifier keeps the wheel open. Releasing over the center/outside the wheel leaves placement unchanged. **Escape** dismisses the wheel until the next gesture.

The seven layouts are:

| Width | Position |
| --- | --- |
| 1/3 | Left, middle, right |
| 1/2 | Left, right |
| 2/3 | Left, right |

The preview and placement use the destination display's usable work area. The menu bar, Dock and taskbar are excluded. Adjacent regions share rounded pixel boundaries, including odd-width and Retina displays; screens left of or above the primary display keep their negative coordinates.

## macOS

Enable the feature in **Settings → General → Window management**. Grant Nashi Accessibility access once through **Enable Accessibility…**. The app checks permission without prompting repeatedly; it never changes macOS privacy settings automatically. Accessibility APIs move and resize the window that was captured at the beginning of the drag.

Upgraded installations retain the `local.tangerine.replica` bundle identity to preserve update and user-data compatibility. macOS may cache the previous Tangerine display name in its Accessibility list; the reference app (`com.th.Tangerine`) is a separate permission. Nashi checks its own `AXIsProcessTrusted()` result. If an old row is enabled but Nashi still reports no access, use **Show Nashi in Finder** to select the current installed app when adding it through macOS settings. Nashi does not reset or edit the privacy database.

The window must actually move or resize before the wheel can activate. Fresh file/web drag payloads, a visible file wheel, editable text controls, scrollbars, minimized windows, full-screen windows and non-resizable/dialog windows are excluded. The overlay does not activate Nashi or capture mouse events from the app being dragged. No window titles, typed content or accessibility trees are recorded.

Some apps enforce minimum dimensions or disallow programmatic resizing. If the requested rectangle cannot be applied, Nashi reports it rather than claiming successful placement.

## Windows

The Home checkbox enables/disables window management and persists outside the portable installation with the user's Nashi preferences. Native `MOVESIZESTART`/`MOVESIZEEND` events identify window gestures; no global keyboard hook or text monitoring is installed. The overlay is non-activating and passes pointer events through to the original app. DWM's invisible window borders are compensated so the visible frame aligns with the target area. Elevated/protected applications may reject resizing.

## Verification

- Swift regression tests cover all seven layouts, odd/Retina/negative screen coordinates, ordinary content drags, fresh-file precedence and Escape cancellation.
- The Windows core verifier covers the same partitions and preference persistence.
- The explicit macOS `--window-management-smoke <manager-pid> <output-directory>` mode runs from the installed Nashi identity after access is granted. It admits only a manager running that same executable and a newly launched disposable Nashi fixture. It tests all seven physical Shift/Command drags, releasing the modifier before mouse-up, and Escape cancellation. Input is released on failure or interruption; the fixture closes and the pointer is restored. Missing access fails before posting any input.
- The Windows native smoke test launches a separate disposable Nashi fixture process, applies all seven layouts using real window APIs, exercises real Shift/Alt title-bar drags and Escape, and captures the wheel. It does not move the user's other windows.
- macOS cross-app drag verification requires the installed Nashi build's Accessibility permission. Compilation and geometry tests do not establish that this permission has been granted or that the live drag path has passed.
