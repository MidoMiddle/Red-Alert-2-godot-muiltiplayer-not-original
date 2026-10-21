using Rts.Client.Controls;

namespace Rts.Simulation.Tests.Client;

/// <summary>Camera bookmarks F1…F4 (docs/implementation/M2_unit_control.md, 4.14).</summary>
public sealed class CameraBookmarksTests
{
    [Fact]
    public void ASavedBookmarkIsReadBackAndAnEmptyOneIsNot()
    {
        var bookmarks = new CameraBookmarks();
        bookmarks.Save(1, 12.5f, 40f);

        Assert.True(bookmarks.TryGet(1, out var point));
        Assert.Equal((12.5f, 40f), point);
        Assert.False(bookmarks.TryGet(2, out _));
    }

    [Fact]
    public void SavingAgainReplacesTheBookmark()
    {
        var bookmarks = new CameraBookmarks();
        bookmarks.Save(4, 1f, 2f);
        bookmarks.Save(4, 30f, 31f);

        Assert.True(bookmarks.TryGet(4, out var point));
        Assert.Equal((30f, 31f), point);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void OnlyFourSlotsExist(int slot)
    {
        var bookmarks = new CameraBookmarks();

        Assert.Throws<ArgumentOutOfRangeException>(() => bookmarks.Save(slot, 0f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => bookmarks.TryGet(slot, out _));
    }
}
