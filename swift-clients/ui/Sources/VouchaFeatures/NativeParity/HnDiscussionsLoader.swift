import Observation
import VouchaAPI
import VouchaCore

@Observable
@MainActor
final class HnDiscussionsLoader {
    var threads: [HnDiscussionThread] = []
    let urls: [String]
    var client: APIClient?
    var searchClient: HnDiscussionsClient

    init(
        urls: [String],
        client: APIClient? = nil,
        searchClient: HnDiscussionsClient = HnDiscussionsClient()
    ) {
        self.urls = urls
        self.client = client
        self.searchClient = searchClient
    }

    var taskID: String {
        "\(client != nil)-\(HnDiscussionURLCollector.collect(urls).joined(separator: "\n"))"
    }

    func load() async {
        threads = []
        guard let client else { return }
        let identity: NativeIdentityResponse
        do {
            identity = try await client.send(.myIdentity)
        } catch {
            return
        }
        guard identity.identity.hnDiscussions == true else { return }
        threads = await searchClient.search(urls: urls)
    }
}
