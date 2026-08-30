using YoutubeDownload.Application.Common;
using YoutubeDownload.Domain.Entities;
using Xunit;

namespace YoutubeDownload.Tests;

public class MediaPlanTests
{
    private static readonly IReadOnlyList<MediaFormatOption> Formats =
    [
        new("18", Domain.Enums.MediaFormat.Mp4, "360p", 360, "mp4", 1_000_000),
        new("22", Domain.Enums.MediaFormat.Mp4, "720p", 720, "mp4", 5_000_000),
        new("137", Domain.Enums.MediaFormat.Mp4, "1080p", 1080, "mp4", 10_000_000),
        new("248", Domain.Enums.MediaFormat.WebM, "1080p", 1080, "webm", 9_000_000)
    ];

    [Theory]
    [InlineData("720p", "22")]
    [InlineData("1080p", "137")]
    [InlineData("480p", "18")]     // never upscale: highest available <= request
    [InlineData("4320p", "137")]   // request above available -> highest
    [InlineData(null, "137")]      // no request -> highest
    public void Select_PicksHighestResolutionAtOrBelowRequest(string? quality, string expectedId)
    {
        var selected = MediaPlan.Select(Formats, "mp4", quality);
        Assert.Equal(expectedId, selected?.Id);
    }

    [Fact]
    public void Select_FiltersByContainer()
    {
        var selected = MediaPlan.Select(Formats, "webm", "1080p");
        Assert.Equal("248", selected?.Id);
    }

    [Fact]
    public void Select_FallsBackToOtherContainer_WhenNoneAvailable()
    {
        var selected = MediaPlan.Select(Formats, "mkv", "720p");
        Assert.NotNull(selected); // graceful fallback rather than null
        Assert.Equal("137", selected!.Id); // best available overall (stable order at 1080p)
    }

    [Fact]
    public void ParseQualityHeight_AcceptsBothNotations()
    {
        Assert.Equal(720, MediaPlan.ParseQualityHeight("720p"));
        Assert.Equal(720, MediaPlan.ParseQualityHeight("p720"));
        Assert.Equal(720, MediaPlan.ParseQualityHeight("720"));
        Assert.Null(MediaPlan.ParseQualityHeight("abc"));
        Assert.Null(MediaPlan.ParseQualityHeight(null));
    }

    [Fact]
    public void TryParseContainer_RecognizesKnownContainers()
    {
        Assert.True(MediaPlan.TryParseContainer("mp4", out var mp4));
        Assert.Equal(Domain.Enums.MediaFormat.Mp4, mp4);
        Assert.False(MediaPlan.TryParseContainer("avi", out _));
    }
}