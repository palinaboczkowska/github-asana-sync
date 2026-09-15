namespace GithubAsanaSync.Functions.Services;

public interface IAsanaClient
{
    // Returns the new task's GID.
    Task<string> CreateTaskAsync(string name, string notes, CancellationToken cancellationToken);

    Task UpdateTaskAsync(string taskGid, bool completed, CancellationToken cancellationToken);

    Task AddCommentAsync(string taskGid, string text, CancellationToken cancellationToken);
}
