using Xunit;
using GithubAsanaSync.Functions.Models;

namespace GithubAsanaSync.Tests;

public class IssueMappingTests
{
    [Theory]
    [InlineData("palinaboczkowska/github-asana-sync", "palinaboczkowska_github-asana-sync")]
    [InlineData("owner/name#1", "owner_name_1")]
    [InlineData("owner/na?me", "owner_na_me")]
    public void BuildPartitionKey_replaces_characters_table_storage_rejects(string repository, string expected)
    {
        Assert.Equal(expected, IssueMapping.BuildPartitionKey(repository));
    }

    [Fact]
    public void BuildPartitionKey_leaves_an_already_safe_name_alone()
    {
        Assert.Equal("plain-name", IssueMapping.BuildPartitionKey("plain-name"));
    }

    [Fact]
    public void BuildRowKey_tags_the_source_kind_so_issue_and_pr_numbers_do_not_collide()
    {
        Assert.NotEqual(
            IssueMapping.BuildRowKey(GitHubSourceKind.Issue, 42),
            IssueMapping.BuildRowKey(GitHubSourceKind.PullRequest, 42));
    }
}
