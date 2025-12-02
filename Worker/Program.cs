using Azure.Storage.Queues;
using Infra.IoC;
using Infrastructure.Services.Storage;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AzureBlobOptions>(
    builder.Configuration.GetSection("AzureBlob"));

builder.Services.AddSingleton<FfmpegRunner>(sp =>
{
    var ffmpegPath = sp.GetRequiredService<IConfiguration>().GetValue<string>("Worker:FfmpegPath") ?? "ffmpeg";
    var ffprobePath = sp.GetRequiredService<IConfiguration>().GetValue<string>("Worker:FfprobePath") ?? "ffprobe";
    var logger = sp.GetRequiredService<ILogger<FfmpegRunner>>();
    return new FfmpegRunner(ffmpegPath, ffprobePath,logger);
});

builder.Services.AddInfrastructureIoC(builder.Configuration);

builder.Services.AddHostedService<MxfWorkerHostedService>();

var host = builder.Build();

host.Run();