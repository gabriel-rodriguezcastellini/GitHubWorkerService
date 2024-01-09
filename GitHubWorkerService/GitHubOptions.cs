namespace GitHubWorkerService
{
    public class GitHubOptions
    {
        public const string GitHub = "GitHub";

        public required string Login { get; set; }

        public required string AccessToken { get; set; }
    }
}
