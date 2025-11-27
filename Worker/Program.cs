var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker.Worker>();

var host = builder.Build();
host.Run();