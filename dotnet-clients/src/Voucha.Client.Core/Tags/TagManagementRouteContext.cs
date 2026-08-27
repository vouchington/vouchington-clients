namespace Voucha.Client.Core.Tags;

public sealed record TagManagementRouteContext(
    string EntityType,
    string EntityIdOrSlug,
    string ObjectType,
    string? TopicType = null);
