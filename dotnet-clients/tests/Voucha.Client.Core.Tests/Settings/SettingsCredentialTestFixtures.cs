using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Settings;

internal static class SettingsCredentialTestFixtures
{
  internal static readonly ScopeCatalogResponse Catalog = new([
      new("rss:read", "api", "rss", "read", ["api-key"], null, null),
      new("mcp.user:read", "user", "mcp.user", "read", ["api-key", "oauth"], "mcp_user_full_access", null),
      new("mcp.user:write", "user", "mcp.user", "write", ["api-key", "oauth"], "mcp_user_full_access", "mcp.user:read"),
      new("mcp.admin:read", "admin", "mcp.admin", "read", ["api-key", "oauth"], "mcp_admin_full_access", null),
      new("oauth-only:read", "user", "oauth-only", "read", ["oauth"], null, null),
  ]);
  internal static readonly OAuthGrantListResponse EmptyGrants = new([], new PageInfo(null, false, null));
}
