using ZipStation.Business.Helpers;
using ZipStation.Models.Enums;
using Xunit;

namespace ZipStation.Tests;

public class ExternalSourceSearchTests
{
    [Fact]
    public void Parse_TwoSegmentForumThreadUrl_TreatsThreadIdAsMessageId()
    {
        var src = ExternalSourceSearch.Parse("https://discord.com/channels/1475012785397039134/1520468533916467302");

        Assert.NotNull(src);
        Assert.Equal(ExternalSourceType.Discord, src!.Type);
        Assert.Equal("1475012785397039134", src.GuildId);
        // 2-segment form: optimistically a forum thread, where threadId == starter messageId.
        Assert.Null(src.ChannelId);
        Assert.Equal("1520468533916467302", src.ThreadId);
        Assert.Equal("1520468533916467302", src.MessageId);
    }

    [Fact]
    public void Parse_ThreeSegmentMessageUrl_SplitsChannelAndMessage()
    {
        var src = ExternalSourceSearch.Parse("https://discord.com/channels/111/222/333");

        Assert.NotNull(src);
        Assert.Equal("111", src!.GuildId);
        Assert.Equal("222", src.ChannelId);
        Assert.Equal("222", src.ThreadId);
        Assert.Equal("333", src.MessageId);
    }

    [Theory]
    [InlineData("https://discordapp.com/channels/111/222")]      // legacy discordapp.com host
    [InlineData("https://canary.discord.com/channels/111/222")]  // client subdomain
    [InlineData("https://discord.com/channels/111/222/")]        // trailing slash
    [InlineData("https://discord.com/channels/111/222?x=1")]     // query string
    public void Parse_AcceptsHostAndTrailingVariants(string url)
    {
        Assert.NotNull(ExternalSourceSearch.Parse(url));
    }

    [Theory]
    [InlineData("just some text")]
    [InlineData("STR-522")]
    [InlineData("https://example.com/channels/111/222")]          // not a discord host
    [InlineData("https://discord.com/channels/abc/def")]          // non-numeric ids
    public void Parse_ReturnsNullForNonExternalSourceQueries(string query)
    {
        Assert.Null(ExternalSourceSearch.Parse(query));
    }

    [Fact]
    public void MatchFilter_BuildsWithoutThrowing()
    {
        // The filter is rendered by the driver at query time; just assert construction is valid.
        var src = ExternalSourceSearch.Parse("https://discord.com/channels/111/222/333")!;
        Assert.NotNull(ExternalSourceSearch.MatchFilter(src));
    }
}
