# Pastille

Pastille is a lightweight, native Windows webcam overlay: keep your camera image
above your applications while presenting or working. Move and resize it, choose
an oval or rounded rectangle, and adjust the framing without moving the window.
It is a WPF/.NET 8 desktop application, with no embedded browser, server or FFmpeg.

The webcam stays on your PC. Pastille does not upload images, record video,
capture the desktop, or access the microphone. It is not a virtual camera and
does not add an overlay to another application's camera feed.

![Pastille's rectangular portrait overlay in the lower-left corner of a browser displaying Have I Been Pwned](docs/images/pastille-desktop-overlay.png)

*Example: a rectangular portrait overlay stays visible over a browser window.*

## Install or update

Download a ZIP from the [Pastille 0.5.3 release](https://github.com/YannMSFT/Pastille/releases/tag/v0.5.3)
that matches your processor:

| Archive | Windows PC |
|---|---|
| [Pastille-x64-0.5.3.zip](https://github.com/YannMSFT/Pastille/releases/download/v0.5.3/Pastille-x64-0.5.3.zip) | Intel or AMD, 64-bit |
| [Pastille-ARM64-0.5.3.zip](https://github.com/YannMSFT/Pastille/releases/download/v0.5.3/Pastille-ARM64-0.5.3.zip) | ARM64, including Snapdragon |
| [Pastille-0.5.3-sources.zip](https://github.com/YannMSFT/Pastille/releases/download/v0.5.3/Pastille-0.5.3-sources.zip) | Source code and guides |
| [Pastille-Guide-0.5.3.md](https://github.com/YannMSFT/Pastille/releases/download/v0.5.3/Pastille-Guide-0.5.3.md) | French user guide |

SHA-256 checksums for all release downloads are published as `SHA256SUMS.txt`
alongside the release assets.

Check **Settings > System > About > System type** if unsure. The releases target
Windows 11 x64 and ARM64, not 32-bit x86. Prefer the native build for your CPU.

1. Quit any running instance of Pastille using its notification-area icon.
2. Right-click the downloaded ZIP and choose **Extract All**. Do not run from
   inside the archive.
3. Keep `Pastille.exe` in a folder of your choice and launch it.

Each ZIP contains a single, self-contained `Pastille.exe`. No installer,
administrator rights, adjacent DLLs, or separate .NET Desktop Runtime installation
are required. The executable is unsigned, so Windows may display a SmartScreen
warning. Follow your organization's security policies on managed PCs.

The bundled runtime makes the executable roughly 70-80 MB. On startup, .NET
automatically extracts native dependencies into its user temporary cache
(`%TEMP%\.net\Pastille` by default), so that location must be writable. Single-file
distribution does not mean the application creates no working files. Runtime
security updates require replacing the executable with a newer published build.

## Start the webcam

Pastille starts in the **notification area** beside the clock, with no settings
window or taskbar button. Its icon is a white, vertical octagonal lozenge with a
subtle bevel on a transparent background. Check the hidden-icons **^** menu if
Windows hides it.

Click the icon, or right-click it and select **Settings…**. Select a webcam and
click **Start webcam**. Use **Refresh** after connecting a device. The overlay is
visible on startup, but the webcam is never activated automatically. Until the
camera starts, and after it stops, the oval or rectangle displays solid chroma
green (`#00FF00`) suitable for use as a green-screen background. The outline
remains visible and the shape retains its normal clipping. **Stop webcam** releases
the camera, clears the last frame and restores the green background.

If access fails, check **Settings > Privacy & security > Camera** and allow
desktop apps to access the camera. Device compatibility and camera sharing depend
on the Windows driver and other applications using the device.

## Overlay controls

| Control | Effect |
|---|---|
| Drag the overlay | Move it on screen |
| Bottom handle | Resize width and height independently |
| Double-click | Open settings |
| Right-click | Open controls, hide the overlay, switch shape, or reset framing |
| Mouse wheel | Adjust zoom inside the overlay |
| **Ctrl+Shift+F9** | Hide or show the overlay globally, without opening settings |

The shortcut works from other applications even when settings are closed.
The settings checkbox, visibility button, tray menu and overlay menu provide
equivalent visibility controls. **Hiding does not stop the webcam**: this allows
instant reappearance. Stop the webcam or quit Pastille to release it.

Settings include:

- **Shape and mirror:** oval or rectangle; equal width and height make the oval a
  circle. Mirroring is enabled by default.
- **Outline:** enable or disable it, choose a color with the Windows color picker,
  and set thickness from 1 to 20 display units. Disabling retains its settings.
- **Rectangle corners:** radius from 0 to 200 display units; 0 means square corners.
  The effective radius is clamped to half the shortest side, and applies to both
  the image and outline. The value is retained in oval mode.
- **Face framing:** zoom from 50% to 400%, plus horizontal and vertical pan.
  Positive pan moves the image right or down. **Reset framing** restores 100%
  zoom and centered framing without changing window size or position.
- **Overlay size:** width from 120 to 960 and height from 90 to 720 display units.
  **Center overlay** moves the window to the center of the working area.

The default framing fills the overlay without distorting the image. Zooming out
below 100% or panning far can reveal the green background. There is no automatic
face tracking, background removal or cutout.

## Tray and quitting

Right-click the notification icon for **Settings…**, **Hide/Show overlay**, or
**Quit Pastille**. Clicking the icon or activating it with Enter/Space opens
settings. Closing or minimizing settings only hides that window: the overlay
and webcam keep their current state.

Use **Quit Pastille** to exit completely. This closes both windows, releases the
camera and global shortcut, and removes the tray icon. Pastille re-registers the
icon if Windows Explorer restarts. If Windows refuses to create the icon during
startup, Pastille reports the error and exits rather than staying inaccessible.

## Language and saved settings

At startup, Pastille uses the current user's **Windows display/UI language**, not
the region setting, to select French, English, Spanish, German or Italian.
Regional variants such as `fr-CA`, `en-GB`, `es-MX`, `de-AT` and `it-CH` use their
base language translations and retain their regional number/percentage formatting.
Unsupported display languages fall back to English, including formatting.
Restart Pastille after changing the Windows display language; there is no separate
in-app language setting.

Settings, menus, tooltips, status text and application-authored errors are
translated. Camera names, native Windows dialogs (including the color picker),
Windows-generated exception details and technical diagnostic codes remain provided
by Windows or the device driver and may use their own language.

Position, size, shape, mirroring, zoom, pan, outline, corner radius and webcam
selection are saved in `%LOCALAPPDATA%\Pastille\settings.json`. Errors are logged
to `%LOCALAPPDATA%\Pastille\errors.log`.

If no Pastille settings exist, the application imports preferences from the
previous installation once, preserving the original settings file. Pastille
settings take priority on subsequent launches. Removed recording
preferences are ignored; existing videos are not modified or deleted. Changing
the UI language does not rename JSON properties or change saved numeric values.

## Limitations

- The overlay stays above ordinary windows, not Windows security screens or
  necessarily exclusive full-screen applications.
- The preview is limited to 960 x 720 and about 25 frames per second, reusing
  its pixel buffer. Hiding the overlay does not suspend capture.
- If another application owns **Ctrl+Shift+F9** (including another Pastille
  instance), settings show a translated conflict message. Close the
  conflicting application and restart Pastille. The buttons and menus still
  work; holding the shortcut does not repeatedly toggle visibility.
- The webcam is local, but other software may capture the visible overlay as
  part of screen sharing or recording. Pastille itself does neither.

## Build and verification

Use the .NET 8 SDK on Windows:

```powershell
dotnet build .\Pastille.csproj -c Release -r win-arm64
dotnet build .\Pastille.csproj -c Release -r win-x64
dotnet publish .\Pastille.csproj -c Release -r win-arm64 -o .\publish-arm64
dotnet publish .\Pastille.csproj -c Release -r win-x64 -o .\publish-x64
```

ARM64 is the default target; `-r win-x64` overrides it. Both published folders
contain only `Pastille.exe`, with compressed managed resources, satellite
translations and native dependencies bundled. Self-contained single-file
publishing and embedded symbols are enabled; trimming is disabled for WPF/WinRT.

Run the published executable on a Windows desktop supporting its architecture:

```powershell
$process = Start-Process .\publish-arm64\Pastille.exe -ArgumentList '--self-test' -Wait -PassThru
$process.ExitCode
```

Substitute `publish-x64` for x64. Self-tests exercise real windows, the tray, global
shortcut messages and the native color picker, so they require an interactive
desktop. They do not activate the webcam or alter user preferences. They cover
CPU architecture, branding, icon rendering, isolated legacy settings migration,
validation and JSON round trips, framing, shape/mirror/resize, outline rendering,
rounded corners, and close-to-tray/exit behavior.
They also check the initial chroma-green fill, active video display, frame clearing
on stop, both overlay shapes, clipping and outline rendering.

Localization coverage checks all resource keys and format arguments in all five
languages without silently accepting missing satellite resources, regional
selection, English fallback, worker culture, localized validation errors and
percentage formatting. Real WPF controls, overlay and tray actions are exercised
in all five languages and for an unsupported language's English fallback.

Results are written beside the executable as `self-test-results.txt`; failure
writes `self-test-error.txt` and exits nonzero. Tests also write
`pastille-icon-preview.png`. Use a writable test directory. To additionally verify
bundled runtime loading, run in a fresh, writable extraction directory:

```powershell
$env:PASTILLE_REQUIRE_BUNDLED_RUNTIME = '1'
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $PWD 'bundle-test-arm64'
$process = Start-Process .\publish-arm64\Pastille.exe -ArgumentList '--self-test' -Wait -PassThru
$process.ExitCode
```

Translations live in standard .NET resources: `Strings.resx` is neutral English;
`Strings.fr.resx`, `Strings.es.resx`, `Strings.de.resx` and `Strings.it.resx` are
satellites. `Localization.cs` shares lookups between XAML and C#. Keep keys and
format placeholders identical across files. `App.OnStartup` selects culture and
WPF language before constructing windows or the tray.

See also the [French guide](LISEZMOI.md) and Microsoft's documentation on
[single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview),
[WPF globalization](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/globalization-for-wpf),
and [MediaFrameReader](https://learn.microsoft.com/windows/uwp/audio-video-camera/process-media-frames-with-mediaframereader).
