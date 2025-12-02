using System.Diagnostics;

namespace Worker;

public sealed class FfmpegRunner
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly ILogger<FfmpegRunner> _logger;

    public FfmpegRunner(string ffmpegPath, string ffprobePath, ILogger<FfmpegRunner> logger)
    {
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
        _logger = logger;
    }

    /// <summary>
    /// Converts inputFile -> outputFile as web-friendly MP4 (H.264/AAC + faststart).
    /// </summary>
    public async Task RunAsync(string inputFile, string outputFile, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var duration = await GetDurationInSeconds(inputFile, ct);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments =
                $"-y -i \"{inputFile}\" " +
                // Vídeo
                "-c:v libx264 -preset veryfast -crf 23 " +
                "-profile:v high -level 4.0 " + // Melhor compatibilidade
                "-pix_fmt yuv420p " + // Necessário para alguns navegadores
                "-vf \"scale='min(1280,iw)':min'(720,ih)':force_original_aspect_ratio=decrease,format=yuv420p\" " +
            
                // Áudio
                "-c:a aac -b:a 192k -ac 2 " + // Estéreo para melhor compatibilidade
            
                // Metadados web
                "-movflags +faststart " +
                "-metadata title=\"Video\" " +
            
                // Otimizações para web
                "-g 30 -keyint_min 30 " + // GOP size para streaming
                "-threads 0 " + // Usa todos os cores
            
                // Saída
                $"-f mp4 \"{outputFile}\" -progress pipe:1 -nostats",
            
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        await RunProcessAsync(psi, duration, progress, ct);
    }

    private async Task RunProcessAsync(ProcessStartInfo psi, double duration, IProgress<double>? progress,
        CancellationToken ct)
    {
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();

        // Read stderr to log
        var stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardError.ReadLineAsync()) != null)
            {
                _logger.LogDebug("[ffmpeg-stderr] {Line}", line);
            }
        }, ct);

        // Read stdout for progress
        string? outLine;
        while ((outLine = await proc.StandardOutput.ReadLineAsync()) != null)
        {
            try
            {
                if (outLine.StartsWith("out_time_ms="))
                {
                    var s = outLine.Substring("out_time_ms=".Length);
                    if (long.TryParse(s, out var outTimeMs) && duration > 0)
                    {
                        var p = Math.Min(1.0, outTimeMs / 1000.0 / duration);
                        progress?.Report(p);
                    }
                }

                if (outLine.StartsWith("progress=") && outLine.EndsWith("end"))
                    progress?.Report(1.0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing ffmpeg progress line: {Line}", outLine);
            }
        }

        await proc.WaitForExitAsync(ct);
        await stderrTask;

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg exited with code {proc.ExitCode}");
    }

    private async Task<double> GetDurationInSeconds(string inputFile, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments =
                    $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{inputFile}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var text = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync(ct);

            if (double.TryParse(text.Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var duration))
                return duration;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe failed to get duration, progress will be best-effort");
        }

        return 0.0;
    }
}
