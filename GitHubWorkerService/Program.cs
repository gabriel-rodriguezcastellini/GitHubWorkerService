using GitHubWorkerService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.GitHub));
builder.Services.Configure<GitHubWorkerService.FileOptions>(builder.Configuration.GetSection(GitHubWorkerService.FileOptions.File));

IHost host = builder.Build();

host.Run();
