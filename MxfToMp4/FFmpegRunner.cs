using System.Diagnostics;

namespace MxfToMp4;

public interface IFFmpegRunner {
    Task ConvertToMp4Async(string inputPath, string outputPath, CancellationToken ct = default);
}

public class FFmpegRunner : IFFmpegRunner {
    private readonly string _ffmpegPath;
    private readonly ILogger<FFmpegRunner> _log;

    public FFmpegRunner(IConfiguration cfg, ILogger<FFmpegRunner> log) {
        _ffmpegPath = cfg["FFMPEG_PATH"] ?? "ffmpeg";
        _log = log;
    }

    public async Task ConvertToMp4Async(string inputPath, string outputPath, CancellationToken ct = default) {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var args = $"-y -i \"{inputPath}\" -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k \"{outputPath}\"";
        var psi = new ProcessStartInfo(_ffmpegPath, args) {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        p.OutputDataReceived += (s, e) => { if (e.Data != null) _log.LogDebug(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using (ct.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } })) {
            await p.WaitForExitAsync(ct);
        }
        if (p.ExitCode != 0) throw new Exception($"ffmpeg exit code {p.ExitCode}");
    }
}