namespace Voucha.Client.Core.Api;

public interface IPageOfUsers
{
  IReadOnlyList<User> Results { get; }

  PageInfo PageInfo { get; }
}
