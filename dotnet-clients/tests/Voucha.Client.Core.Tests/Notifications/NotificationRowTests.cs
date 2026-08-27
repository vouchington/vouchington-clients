using Voucha.Client.Core.Notifications;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationRowTests
{
  [Theory]
  [InlineData(false, true, "Unread", "Mark read")]
  [InlineData(true, false, "Read", "Read")]
  public void ReadStateLabelsMatchRowState(
      bool isRead,
      bool isUnread,
      string readStateLabel,
      string markReadActionLabel)
  {
    var row = new NotificationRow(
        "n1",
        UiText.UserContent("Title"),
        "Body",
        new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
        "/my/notifications",
        "post",
        isRead);

    Assert.Equal(isUnread, row.IsUnread);
    Assert.Equal(readStateLabel, row.ReadStateLabel);
    Assert.Equal(markReadActionLabel, row.MarkReadActionLabel);
    Assert.Equal("Title", row.LocalizedTitle);
    Assert.Equal("Body", row.UserContentBody);
    Assert.Equal("/my/notifications", row.TargetPath);
    Assert.Equal("post", row.EntityType);
  }
}
