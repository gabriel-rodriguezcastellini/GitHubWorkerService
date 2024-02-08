using Microsoft.Extensions.Options;
using Octokit;
using System.Text;

namespace GitHubWorkerService
{
    public class Worker(ILogger<Worker> logger, IOptions<GitHubOptions> gitHubOptions, IOptions<FileOptions> fileOptions) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GitHubWorkerService");

                    if (!Directory.Exists(path))
                    {
                        _ = Directory.CreateDirectory(path);
                    }

                    path = Path.Combine(path, fileOptions.Value.FileName);

                    if (File.Exists(path) && File.GetCreationTime(path).Date == DateTime.Now.Date)
                    {
                        await Task.Delay(new TimeSpan(days: 1, 0, 0, 0), stoppingToken);

                        return;
                    }

                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    }

                    GitHubClient github = new(new ProductHeaderValue(gitHubOptions.Value.Login)) { Credentials = new Credentials(gitHubOptions.Value.AccessToken) };
                    User user = await github.User.Get(gitHubOptions.Value.Login);
                    Console.WriteLine(user.Followers + " folks love me!");
                    Dictionary<string, long> languages = [];

                    foreach (Repository? item in await github.Repository.GetAllForCurrent())
                    {
                        (await github.Repository.GetAllLanguages(item.Id)).ToList().ForEach(x =>
                        {
                            if (languages.ContainsKey(x.Name))
                            {
                                languages[x.Name] += x.NumberOfBytes;
                            }
                            else
                            {
                                languages.Add(x.Name, x.NumberOfBytes);
                            }
                        });
                    }

                    using (FileStream fs = File.Create(path, 1024))
                    {
                        byte[] info = new UTF8Encoding(true).GetBytes(string.Join(Environment.NewLine, languages.OrderByDescending(x => x.Value).Select(x => $"{x.Key} - {x.Value}")));
                        await fs.WriteAsync(info, stoppingToken);
                    }

                    await Task.Delay(new TimeSpan(days: 1, 0, 0, 0), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // When the stopping token is canceled, for example, a call made from services.msc,
                // we shouldn't exit with a non-zero exit code. In other words, this is expected...
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Message}", ex.Message);

                // Terminates this process and returns an exit code to the operating system.
                // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
                // performs one of two scenarios:
                // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
                // 2. When set to "StopHost": will cleanly stop the host, and log errors.
                //
                // In order for the Windows Service Management system to leverage configured
                // recovery options, we need to terminate the process with a non-zero exit code.
                Environment.Exit(1);
            }
        }
    }
}
