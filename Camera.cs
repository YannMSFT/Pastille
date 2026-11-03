using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Threading;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace Pastille;

internal sealed class Camera : IAsyncDisposable
{
    private MediaCapture? capture;
    private MediaFrameReader? reader;
    private readonly Dispatcher dispatcher;
    private int framePending;
    private volatile bool stopping;
    private long previousFrameTime;
    private byte[]? frameBuffer;
    private WriteableBitmap? preview;
    public event Action<BitmapSource>? Frame;
    public event Action<string>? Failed;

    public Camera(Dispatcher dispatcher) => this.dispatcher = dispatcher;

    public static async Task<DeviceInformationCollection> DevicesAsync() =>
        await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

    public async Task StartAsync(string deviceId)
    {
        try
        {
            capture = new MediaCapture();
            capture.Failed += CaptureFailed;
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                VideoDeviceId = deviceId, StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly
            });
            var source = capture.FrameSources.Values.FirstOrDefault(x =>
                x.Info.SourceKind == MediaFrameSourceKind.Color &&
                x.Info.MediaStreamType == MediaStreamType.VideoPreview)
                ?? capture.FrameSources.Values.FirstOrDefault(x => x.Info.SourceKind == MediaFrameSourceKind.Color)
                ?? throw new InvalidOperationException("Cette webcam ne fournit pas de flux vidéo couleur compatible.");
            var format = source.CurrentFormat.VideoFormat;
            var scale = Math.Min(1, Math.Min(960d / format.Width, 720d / format.Height));
            reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8,
                new BitmapSize
                {
                    Width = Math.Max(2, (uint)(format.Width * scale)),
                    Height = Math.Max(2, (uint)(format.Height * scale))
                });
            reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            reader.FrameArrived += FrameArrived;
            var status = await reader.StartAsync();
            if (status != MediaFrameReaderStartStatus.Success)
                throw new InvalidOperationException($"Impossible de démarrer la webcam : {status}.");
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    private void CaptureFailed(MediaCapture sender, MediaCaptureFailedEventArgs args) =>
        dispatcher.BeginInvoke(() => Failed?.Invoke(args.Message));

    private void FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        var now = Environment.TickCount64;
        if (stopping || now - Interlocked.Read(ref previousFrameTime) < 40 ||
            Interlocked.CompareExchange(ref framePending, 1, 0) != 0) return;
        Interlocked.Exchange(ref previousFrameTime, now);
        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            using var original = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (original is null) { Interlocked.Exchange(ref framePending, 0); return; }
            using var bitmap = SoftwareBitmap.Convert(original, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var length = width * height * 4;
            if (frameBuffer is null || frameBuffer.Length != length) frameBuffer = new byte[length];
            var buffer = frameBuffer;
            bitmap.CopyToBuffer(buffer.AsBuffer());
            dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                try
                {
                    if (stopping) return;
                    if (preview is null || preview.PixelWidth != width || preview.PixelHeight != height)
                        preview = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                    preview.WritePixels(new Int32Rect(0, 0, width, height), buffer, width * 4, 0);
                    Frame?.Invoke(preview);
                }
                finally { Interlocked.Exchange(ref framePending, 0); }
            });
        }
        catch (Exception ex)
        {
            stopping = true;
            Interlocked.Exchange(ref framePending, 0);
            App.Log(ex);
            dispatcher.BeginInvoke(() => Failed?.Invoke(ex.Message));
        }
    }

    public async ValueTask DisposeAsync()
    {
        stopping = true;
        try
        {
            if (reader is not null)
            {
                reader.FrameArrived -= FrameArrived;
                try { await reader.StopAsync(); }
                finally { reader.Dispose(); reader = null; }
            }
        }
        finally
        {
            if (capture is not null)
            {
                capture.Failed -= CaptureFailed;
                capture.Dispose();
                capture = null;
            }
        }
    }
}
