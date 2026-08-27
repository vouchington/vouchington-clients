import VouchaAPI

extension EndpointManifestCoverage {
    static let entityRelationEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.entity-relations.post.category.topic.default") {
            Endpoint.entityRelations(
                entityType: "post",
                entityId: "post-1",
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 1,
                after: "fixture-relation-scope-and-sort-cursor"
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.post.related.post.default") {
            Endpoint.entityRelations(
                entityType: "post",
                entityId: "post-1",
                predicate: "related",
                objectType: "post",
                sort: "best",
                limit: 100
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.post.related.url.default") {
            Endpoint.entityRelations(
                entityType: "post",
                entityId: "post-1",
                predicate: "related",
                objectType: "url",
                sort: "best",
                limit: 100
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.topic.publisher-type.topic.default") {
            Endpoint.entityRelations(
                entityType: "topic",
                entityId: "topic-1",
                predicate: "publisher_type",
                objectType: "topic",
                sort: "best",
                limit: 100
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.rss-feed-item.category.topic.default") {
            Endpoint.entityRelations(
                entityType: "rss_feed_item",
                entityId: "item-1",
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 100
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.post.category.topic.create.default") {
            Endpoint.createEntityRelation(
                entityType: "post",
                entityId: "post-1",
                predicate: "category",
                objectType: "topic",
                objectId: "topic-2"
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.user.category.topic.default") {
            Endpoint.entityRelations(
                entityType: "user",
                entityId: "user-abc",
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 100,
                positiveNetVoteScore: true
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.user.category.topic.create.default") {
            Endpoint.createEntityRelation(
                entityType: "user",
                entityId: "user-abc",
                predicate: "category",
                objectType: "topic",
                objectId: "user-tag-bot"
            )
        },
        ManifestRegisteredEndpoint(id: "native.entity-relations.post.category.topic.vote.default") {
            Endpoint.voteEntityRelation(relationId: "relation-post-category-topic-1", choice: .confirm)
        }
    ]
}
