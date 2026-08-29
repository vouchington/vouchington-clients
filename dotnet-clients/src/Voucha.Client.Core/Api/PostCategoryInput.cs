using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TopicPostCategoryInput), "topic")]
[JsonDerivedType(typeof(HashtagPostCategoryInput), "hashtag")]
public abstract record PostCategoryInput;

public sealed record TopicPostCategoryInput(
    [property: JsonPropertyName("topic_id")] string TopicId) : PostCategoryInput;

public sealed record HashtagPostCategoryInput(
    [property: JsonPropertyName("hashtag")] string Hashtag) : PostCategoryInput;
