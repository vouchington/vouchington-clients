import VouchaAPI
import VouchaCore
import VouchaModels

@MainActor
extension NativeTopicManagementViewModel {
    func updateSource(enabled: Bool? = nil, discoverable: Bool? = nil) async {
        guard let client, let feed = sourceDetail else { return }
        do {
            let response: RssFeedUpdateResponse = try await client.send(.updateRssFeed(
                rssFeedId: feed.id,
                body: NativeRssFeedUpdateBody(
                    enabled: enabled,
                    discoverable: discoverable
                )
            ))
            sourceDetail = response.rssFeed
            try await reloadSource()
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func createTopic() async {
        guard let client else { return }
        state = .loading
        do {
            let response: TopicEnvelope = try await client.send(.createTopic(body: createBody))
            apply(topic: response.topic)
            if let followUpBody = createFollowUpBody {
                let updated: TopicEnvelope = try await client.send(.updateTopic(
                    id: response.topic.id,
                    body: followUpBody
                ))
                apply(topic: updated.topic)
            }
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func updateTopic() async {
        guard let client, let topicIdentifier else { return }
        state = .loading
        do {
            let response: TopicEnvelope = try await client.send(.updateTopic(id: topicIdentifier, body: updateBody))
            apply(topic: response.topic)
            try await loadSectionData()
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func mergeTopic() async {
        guard let client, let topicIdentifier else { return }
        let destination = destinationIdOrSlug.trimmed
        guard !destination.isEmpty else { return }
        state = .loading
        do {
            let response: TopicEnvelope = try await client.send(.mergeTopicAliases(
                sourceTopicId: topicIdentifier,
                destinationIdOrSlug: destination
            ))
            apply(topic: response.topic)
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    var createBody: CreateTopicBody {
        .init(
            name: name.trimmed,
            slug: slug.trimmed,
            topicType: topicType.trimmedOrNil,
            markdown: markdown.trimmedOrNil,
            hostname: hostname.trimmedOrNil
        )
    }

    var updateBody: UpdateTopicBody {
        .init(
            name: name.trimmedOrNil,
            slug: slug.trimmedOrNil,
            markdown: markdown.trimmedOrNil,
            topicType: topicType.trimmedOrNil,
            noindex: noindex,
            allowReviews: allowReviews,
            hostname: hostname.isEmpty ? .null : .value(hostname.trimmed),
            logoImageId: logoImageId.isEmpty ? .null : .value(logoImageId.trimmed),
            heroImageId: heroImageId.isEmpty ? .null : .value(heroImageId.trimmed)
        )
    }

    var createFollowUpBody: UpdateTopicBody? {
        let body = UpdateTopicBody(
            markdown: markdown.trimmedOrNil,
            noindex: noindex ? true : nil,
            allowReviews: allowReviews,
            logoImageId: logoImageId.trimmedOrNil.map(NullableStringPatchField.value),
            heroImageId: heroImageId.trimmedOrNil.map(NullableStringPatchField.value)
        )
        guard body.markdown != nil || body.noindex != nil || body.allowReviews != nil ||
            body.logoImageId != nil || body.heroImageId != nil
        else {
            return nil
        }
        return body
    }

    func loadSectionData() async throws {
        switch section {
        case .domains:
            try await reloadAdditionalHostnames()
        case .aliases:
            try await reloadAliases()
        case .source:
            try await reloadSource()
        default:
            break
        }
    }

    func reloadAdditionalHostnames() async throws {
        guard let client, let topicIdentifier else { return }
        let response: TopicAdditionalHostnamesResponse = try await client.send(
            .topicAdditionalHostnames(topicId: topicIdentifier)
        )
        additionalHostnamesPagination.reset(items: response.results)
        additionalHostnamesPagination.restoreContinuation(
            endCursor: response.pageInfo.endCursor,
            hasMore: response.pageInfo.hasNextPage
        )
    }

    func reloadAliases() async throws {
        guard let client, let topicIdentifier else { return }
        let response: Page<String> = try await client.send(.topicAliases(topicId: topicIdentifier))
        aliasesPagination.reset(items: response.results.map(TopicAliasItem.init))
        aliasesPagination.restoreContinuation(
            endCursor: response.pageInfo.endCursor,
            hasMore: response.pageInfo.hasNextPage
        )
    }

    func reloadSource() async throws {
        guard let client, let topicIdentifier else { return }
        let feeds: NativeRssFeedsPageResponse = try await client.send(.rssFeeds(
            topicIdentifier: topicIdentifier,
            enabled: .all
        ))
        guard let feed = feeds.results.first else {
            sourceDetail = nil
            return
        }
        let response: RssFeedDetailResponse = try await client.send(.rssFeed(id: feed.id))
        sourceDetail = response.rssFeed
    }

    func apply(topic: Topic) {
        self.topic = topic
        name = topic.name
        slug = topic.slug
        markdown = topic.markdown ?? ""
        topicType = topic.topicType
        hostname = topic.hostname?.hostname ?? ""
        logoImageId = topic.logoImageId ?? ""
        heroImageId = topic.heroImageId ?? ""
        noindex = topic.noindex ?? false
        allowReviews = topic.allowReviews ?? false
        aliases = topic.aliases ?? []
    }
}
