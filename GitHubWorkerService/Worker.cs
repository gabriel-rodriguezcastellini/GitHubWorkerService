using Microsoft.Extensions.Options;
using Octokit;
using System.Text;

namespace GitHubWorkerService
{
    public class Worker(ILogger<Worker> logger, IOptions<GitHubOptions> gitHubOptions, IOptions<FileOptions> fileOptions) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly GitHubOptions _gitHubOptions = gitHubOptions.Value;
        private readonly FileOptions _fileOptions = fileOptions.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GitHubWorkerService");

                if (!Directory.Exists(path))
                {
                    _ = Directory.CreateDirectory(path);
                }

                path = Path.Combine(path, _fileOptions.FileName);

                if (File.Exists(path) && File.GetCreationTime(path).Date == DateTime.Now.Date)
                {
                    await Task.Delay(new TimeSpan(days: 1, 0, 0, 0), stoppingToken);

                    return;
                }

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }

                GitHubClient github = new(new ProductHeaderValue(_gitHubOptions.Login)) { Credentials = new Credentials(_gitHubOptions.AccessToken) };
                User user = await github.User.Get(_gitHubOptions.Login);
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
    }
}
