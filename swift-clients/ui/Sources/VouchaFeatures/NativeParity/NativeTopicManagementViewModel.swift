import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

enum NativeTopicManagementSection: String {
    case create
    case about
    case behavior
    case domains
    case source
    case aliases
    case merge
}

struct NativeRssFeedUpdateBody: Encodable {
    let enabled: Bool?
    let discoverable: Bool?
}

@Observable
@MainActor
final class NativeTopicManagementViewModel {
    var topic: Topic?
    var name = ""
    var slug = ""
    var markdown = ""
    var topicType = ""
    var hostname = ""
    var logoImageId = ""
    var heroImageId = ""
    var logoImagePlacement: ImagePlacement?
    var heroImagePlacement: ImagePlacement?
    var noindex = false
    var allowReviews = false
    var additionalHostname = ""
    var additionalHostnamesPagination = CursorPaginationState<TopicAdditionalHostname>()
    var aliasesPagination = CursorPaginationState<TopicAlias>()
    var aliasDraft = ""
    var destinationIdOrSlug = ""
    var state: LoadState = .idle
    var sourceDetail: RssFeedDetail?

    let client: APIClient?
    let routeMatch: NativeRouteMatch?

    init(client: APIClient?, routeMatch: NativeRouteMatch?) {
        self.client = client
        self.routeMatch = routeMatch
    }

    var section: NativeTopicManagementSection {
        switch routeMatch?.path {
        case "/topics/create":
            .create
        case let path where path?.hasSuffix("/settings/about") == true:
            .about
        case let path where path?.hasSuffix("/settings/behavior") == true:
            .behavior
        case let path where path?.hasSuffix("/settings/domains") == true:
            .domains
        case let path where path?.hasSuffix("/settings/source") == true:
            .source
        case let path where path?.hasSuffix("/settings/aliases") == true:
            .aliases
        case let path where path?.hasSuffix("/settings/merge") == true:
            .merge
        default:
            .about
        }
    }

    var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    var topicIdentifier: String? {
        routeMatch?.param("idOrSlug", "id")
    }

    func load() async {
        guard !isLoading else { return }
        guard section != .create else {
            state = .loaded
            return
        }
        guard let client, let topicIdentifier else { return }

        state = .loading
        do {
            let response: TopicEnvelope = try await client.send(.topic(id: topicIdentifier))
            apply(topic: response.topic)
            try await loadSectionData()
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func save() async {
        guard !isLoading else { return }
        switch section {
        case .create:
            await createTopic()
        case .about, .behavior, .domains, .source, .aliases:
            await updateTopic()
        case .merge:
            await mergeTopic()
        }
    }

    func addAlias() async {
        guard let client, let topicIdentifier else { return }
        let trimmed = aliasDraft.trimmed
        guard !trimmed.isEmpty else { return }
        do {
            let _: EmptyResponse = try await client.send(.createTopicAliases(
                topicId: topicIdentifier,
                aliases: trimmed
            ))
            aliasDraft = ""
            try await reloadAliases()
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func removeAlias(_ alias: TopicAlias) async {
        guard let client, let topicIdentifier else { return }
        do {
            let _: EmptyResponse = try await client.send(
                .deleteTopicAlias(topicId: topicIdentifier, aliasId: alias.id)
            )
            try await reloadAliases()
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func addAdditionalHostname() async {
        guard let client, let topicIdentifier else { return }
        let trimmed = additionalHostname.trimmed
        guard !trimmed.isEmpty else { return }
        do {
            let _: EmptyResponse = try await client.send(
                .createTopicAdditionalHostname(topicId: topicIdentifier, hostname: trimmed)
            )
            additionalHostname = ""
            try await reloadAdditionalHostnames()
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func removeAdditionalHostname(_ hostnameId: String) async {
        guard let client, let topicIdentifier else { return }
        do {
            let _: EmptyResponse = try await client.send(
                .deleteTopicAdditionalHostname(topicId: topicIdentifier, hostnameId: hostnameId)
            )
            try await reloadAdditionalHostnames()
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

}

extension NativeTopicManagementViewModel {
    func toggleSourceEnabled() async {
        guard let feed = sourceDetail else { return }
        await updateSource(enabled: !feed.isEnabled)
    }

    func toggleSourceDiscoverable() async {
        guard let feed = sourceDetail else { return }
        await updateSource(discoverable: !feed.isDiscoverable)
    }
}
