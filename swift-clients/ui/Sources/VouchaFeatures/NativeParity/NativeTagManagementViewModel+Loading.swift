import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeTagManagementViewModel {
    func load() async {
        guard !isLoading else { return }
        guard let client else { return }

        state = .loading
        // A full reload (e.g. returning to this page after upgrading) must not keep hiding the
        // add-tag form behind a cap reached on a prior visit -- re-derive it from a fresh mutation.
        tagLimitReached = false
        do {
            try await loadSubject(client: client)
            try await loadTabs(client: client)
            syncActiveTab()
            guard activeTabConfig != nil else {
                relations = []
                electionVotes = [:]
                publisherTypes = []
                state = .error(.notFound)
                return
            }
            try await loadRelations(client: client)
            state = .loaded
        } catch is CancellationError {
            state = relations.isEmpty ? .idle : .loaded
        } catch let error as VouchaError {
            if Task.isCancelled {
                state = relations.isEmpty ? .idle : .loaded
                return
            }
            state = .error(error)
        } catch {
            if Task.isCancelled {
                state = relations.isEmpty ? .idle : .loaded
                return
            }
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func reloadRelations() async {
        guard let client, !subjectId.isEmpty, let config = activeTabConfig else { return }
        do {
            try await loadRelations(client: client, config: config)
            if config.predicate == "publisher_type", publisherTypes.isEmpty {
                try await loadPublisherTypes(client: client)
            }
        } catch is CancellationError {
            state = relations.isEmpty ? .idle : .loaded
        } catch let error as VouchaError {
            if Task.isCancelled {
                state = relations.isEmpty ? .idle : .loaded
                return
            }
            relations = []
            electionVotes = [:]
            state = .error(error)
        } catch {
            if Task.isCancelled {
                state = relations.isEmpty ? .idle : .loaded
                return
            }
            relations = []
            electionVotes = [:]
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func loadSubject(client: APIClient) async throws {
        switch subjectKind {
        case .post:
            try await loadPostSubject(client: client)
        case .topic:
            try await loadTopicSubject(client: client)
        case .rssFeedItem:
            subjectId = explicitSubjectId ?? routeMatch?.param("id") ?? ""
            subjectTitle = explicitSubjectTitle.map(UiVerbatimText.verbatim)
                ?? .message(.nativeSwiftTagManagementRssItem)
            subjectDetail = .message(.nativeSwiftTagManagementCategories)
        case .user:
            subjectId = explicitSubjectId ?? routeMatch?.param("idOrUsername", "id") ?? ""
            subjectTitle = .verbatim(explicitSubjectTitle ?? subjectId)
            subjectDetail = .message(.nativeSwiftTagManagementUserTags)
        }
    }

    func loadPostSubject(client: APIClient) async throws {
        let raw = explicitSubjectId ?? routeMatch?.param("id") ?? routeMatch?.path.routeLastSegment ?? ""
        let response: NativePostDetailResponse = try await client.send(.post(idOrSlug: raw))
        subjectId = response.post.id
        subjectTitle = .verbatim(explicitSubjectTitle ?? response.post.title ?? response.post.slug ?? raw)
        subjectDetail = .message(response.post.postType.titleKey)
    }

    func loadTopicSubject(client: APIClient) async throws {
        let raw = explicitSubjectId ?? routeMatch?.param("idOrSlug", "id") ?? routeMatch?.path.routeLastSegment ?? ""
        let response: NativeTopicDetailResponse = try await client.send(.topic(id: raw))
        subjectId = response.topic.id
        subjectTitle = .verbatim(explicitSubjectTitle ?? response.topic.name ?? response.topic.slug ?? raw)
        subjectDetail = response.topic.topicType.map(UiVerbatimText.verbatim)
            ?? .message(.nativeSwiftTopicRecommendationTopic)
        topicType = response.topic.topicType
    }

    func loadTabs(client: APIClient) async throws {
        tabs = switch subjectKind {
        case .post:
            nativePostTagTabs
        case .topic:
            nativeTopicTagTabs(for: topicType)
        case .rssFeedItem:
            nativeRssFeedItemTagTabs
        case .user:
            nativeUserTagTabs
        }

        if let requested = routeMatch?.param("objectType", "object-type") {
            activeTab = tabs.contains(where: { $0.value == requested }) ? requested : ""
        } else if activeTab.isEmpty {
            activeTab = tabs.first?.value ?? ""
        }
        if activeTab == "publisher_type" {
            try await loadPublisherTypes(client: client)
        } else if subjectKind == .user {
            try await loadUserTags(client: client)
        }
    }

    func syncActiveTab() {
        if let requested = routeMatch?.param("objectType", "object-type"),
           !tabs.contains(where: { $0.value == requested }) {
            activeTab = ""
            return
        }
        if activeTab.isEmpty {
            activeTab = tabs.first?.value ?? ""
        }
        if !tabs.contains(where: { $0.value == activeTab }) {
            activeTab = tabs.first?.value ?? ""
        }
    }
}
