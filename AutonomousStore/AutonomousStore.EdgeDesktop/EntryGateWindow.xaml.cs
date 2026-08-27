using AutonomousStore.EdgeDesktop.Models;
using AutonomousStore.EdgeDesktop.Services;
using Microsoft.Extensions.Configuration;
using OpenCvSharp.WpfExtensions;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using ZXing;
using ZXing.Common;
using Cv2 = OpenCvSharp.Cv2;
using Mat = OpenCvSharp.Mat;
using VideoCapture = OpenCvSharp.VideoCapture;
using VideoCaptureProperties = OpenCvSharp.VideoCaptureProperties;
using ColorConversionCodes = OpenCvSharp.ColorConversionCodes;
using RGBLuminanceSource = ZXing.RGBLuminanceSource;

namespace AutonomousStore.EdgeDesktop;

public partial class EntryGateWindow : Window
{
    private readonly ISessionApiService _sessionApiService;
    private readonly int _cameraIndex;

    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private Task? _captureLoopTask;
    private readonly SemaphoreSlim _confirmSemaphore = new(1, 1);

    private readonly BarcodeReaderGeneric _qrReader = new()
    {
        AutoRotate = true,
        Options = new DecodingOptions
        {
            TryHarder = true,
            PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
        }
    };

    // trava anti-repetição — cooldown maior que o tempo de resposta da API
    private static readonly TimeSpan SameTokenCooldown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MinGapBetweenRequests = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, DateTime> _cooldownsUtc = new(StringComparer.Ordinal);
    private DateTime _globalCooldownUtc = DateTime.MinValue;

    // overlay hide
    private CancellationTokenSource? _overlayCts;

    public EntryGateWindow(ISessionApiService sessionApiService, IConfiguration configuration)
    {
        InitializeComponent();
        _sessionApiService = sessionApiService;
        _cameraIndex = configuration.GetValue<int>("Camera:EntryGateCameraIndex", 0);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        StartCamera();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        await StopCameraAsync();
    }

    private void StartCamera()
    {
        _cts = new CancellationTokenSource();

        _capture = new VideoCapture(_cameraIndex);
        if (!_capture.IsOpened())
        {
            // Mostra erro na tela em vez de crashar
            Overlay.Visibility = Visibility.Visible;
            Overlay.Background = new SolidColorBrush(Color.FromRgb(80, 0, 0));
            OverlayTitle.Text = "CÂMERA NÃO ENCONTRADA";
            OverlayMessage.Text = $"Não foi possível abrir a câmera índice {_cameraIndex}.\nVerifique o appsettings.json (Camera:EntryGateCameraIndex).";
            return;
        }

        // tenta reduzir lag (nem toda câmera respeita)
        _capture.Set(VideoCaptureProperties.BufferSize, 1);

        _captureLoopTask = Task.Run(() => CaptureLoopAsync(_cts.Token));
    }

    private async Task StopCameraAsync()
    {
        try
        {
            _cts?.Cancel();

            if (_captureLoopTask is not null)
                await _captureLoopTask;
        }
        catch
        {
            // ignoramos erros no shutdown (fechando a janela)
        }
        finally
        {
            _captureLoopTask = null;

            _capture?.Release();
            _capture?.Dispose();
            _capture = null;

            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        using var frame = new Mat();
        using var rgb = new Mat();

        var lastDecodeUtc = DateTime.MinValue;
        var decodeInterval = TimeSpan.FromMilliseconds(160); // ~6 leituras por segundo

        while (!ct.IsCancellationRequested)
        {
            if (_capture is null)
            {
                await Task.Delay(50, ct);
                continue;
            }

            var ok = _capture.Read(frame);
            if (!ok || frame.Empty())
            {
                await Task.Delay(10, ct);
                continue;
            }

            // 1) renderiza vídeo
            var bmp = BitmapSourceConverter.ToBitmapSource(frame);
            bmp.Freeze(); // para poder atribuir do thread de UI com segurança

            await Dispatcher.InvokeAsync(() =>
            {
                VideoImage.Source = bmp;
            });

            // 2) decodifica QR em intervalos (não em todo frame)
            var nowUtc = DateTime.UtcNow;
            if (nowUtc - lastDecodeUtc < decodeInterval)
                continue;

            lastDecodeUtc = nowUtc;

            var token = TryDecodeQrToken(frame, rgb);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            _ = HandleTokenAsync(token.Trim(), ct);
        }
    }

    private string? TryDecodeQrToken(Mat bgrFrame, Mat rgbBuffer)
    {
        try
        {
            Cv2.CvtColor(bgrFrame, rgbBuffer, ColorConversionCodes.BGR2RGB);

            var width = rgbBuffer.Width;
            var height = rgbBuffer.Height;
            var bytes = checked(width * height * 3); // RGB24 (3 bytes por pixel)
            var buffer = new byte[bytes];

            Marshal.Copy(rgbBuffer.Data, buffer, 0, buffer.Length);

            var source = new RGBLuminanceSource(buffer, width, height, RGBLuminanceSource.BitmapFormat.RGB24);
            var result = _qrReader.Decode(source);
            return result?.Text;
        }
        catch
        {
            // se falhar em um frame, seguimos normalmente
            return null;
        }
    }

    private bool ShouldProcessToken(string token, DateTime nowUtc)
    {
        if (nowUtc < _globalCooldownUtc)
            return false;

        // limpeza simples (evita crescer infinito)
        if (_cooldownsUtc.Count > 200)
        {
            foreach (var kv in _cooldownsUtc)
            {
                if (kv.Value < nowUtc)
                    _cooldownsUtc.TryRemove(kv.Key, out _);
            }
        }

        if (_cooldownsUtc.TryGetValue(token, out var untilUtc) && nowUtc < untilUtc)
            return false;

        _cooldownsUtc[token] = nowUtc.Add(SameTokenCooldown);
        _globalCooldownUtc = nowUtc.Add(MinGapBetweenRequests);
        return true;
    }

    private async Task HandleTokenAsync(string token, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        if (!ShouldProcessToken(token, nowUtc))
            return;

        // evita mais de 1 chamada simultânea (se a API ficar lenta)
        if (!await _confirmSemaphore.WaitAsync(0, ct))
            return;

        try
        {
            var result = await _sessionApiService.ConfirmEntryAsync(token, ct);

            await Dispatcher.InvokeAsync(() =>
            {
                ShowOverlay(result);
            });
        }
        catch (OperationCanceledException)
        {
            // fechando a janela
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ShowOverlay(new ConfirmEntryResult(false, $"Falha ao chamar API: {ex.Message}"));
            });
        }
        finally
        {
            _confirmSemaphore.Release();
        }
    }

    private void ShowOverlay(ConfirmEntryResult result)
    {
        _overlayCts?.Cancel();
        _overlayCts?.Dispose();
        _overlayCts = new CancellationTokenSource();

        Overlay.Visibility = Visibility.Visible;

        if (result.Allowed)
        {
            Overlay.Background = new SolidColorBrush(Color.FromRgb(0, 150, 0));
            OverlayTitle.Text = string.IsNullOrWhiteSpace(result.CustomerName) ? "ENTRADA LIBERADA" : result.CustomerName!;
            OverlayMessage.Text = string.IsNullOrWhiteSpace(result.Message) ? "Entrada liberada." : result.Message;
        }
        else
        {
            Overlay.Background = new SolidColorBrush(Color.FromRgb(180, 0, 0));
            OverlayTitle.Text = "ACESSO NEGADO";
            OverlayMessage.Text = string.IsNullOrWhiteSpace(result.Message) ? "Entrada recusada." : result.Message;
        }

        // some sozinho depois de alguns segundos (pra voltar a mostrar o vídeo limpo)
        _ = HideOverlayLaterAsync(_overlayCts.Token);
    }

    private async Task HideOverlayLaterAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), ct);
            await Dispatcher.InvokeAsync(() => Overlay.Visibility = Visibility.Collapsed);
        }
        catch (OperationCanceledException)
        {
            // overlay foi renovado
        }
    }
}
