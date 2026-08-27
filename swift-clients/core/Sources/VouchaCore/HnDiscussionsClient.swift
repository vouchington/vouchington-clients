import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

public struct HnDiscussionThread: Equatable, Sendable {
    public let objectID: String
    public let title: String
    public let score: Int
    public let commentCount: Int
    public let itemURL: URL

    public init(objectID: String, title: String, score: Int, commentCount: Int, itemURL: URL) {
        self.objectID = objectID
        self.title = title
        self.score = score
        self.commentCount = commentCount
        self.itemURL = itemURL
    }
}

public struct HnDiscussionsClient: Sendable {
    public static let searchOrigin = URL(string: "https://hn.algolia.com")!
    private let session: URLSession

    public init(session: URLSession = .shared) {
        self.session = session
    }

    public func search(urls: [String]) async -> [HnDiscussionThread] {
        var seen = Set<String>()
        var threads: [HnDiscussionThread] = []
        for url in HnDiscussionURLCollector.collect(urls) {
            for thread in await search(url: url) where seen.insert(thread.objectID).inserted {
                threads.append(thread)
            }
        }
        return threads
    }

    public func search(url: String) async -> [HnDiscussionThread] {
        guard let requestURL = Self.searchURL(for: url) else { return [] }
        do {
            let (data, response) = try await session.data(for: URLRequest(url: requestURL))
            guard let http = response as? HTTPURLResponse, (200 ..< 300).contains(http.statusCode) else {
                return []
            }
            return Self.mapHits(data, sourceURL: url)
        } catch {
            return []
        }
    }

    public static func searchURL(for pageURL: String) -> URL? {
        var components = URLComponents(string: "https://hn.algolia.com/api/v1/search")
        components?.queryItems = [
            URLQueryItem(name: "query", value: pageURL),
            URLQueryItem(name: "restrictSearchableAttributes", value: "url"),
            URLQueryItem(name: "tags", value: "story"),
            URLQueryItem(name: "hitsPerPage", value: "5")
        ]
        return components?.url
    }

    public static func mapHits(_ data: Data, sourceURL: String) -> [HnDiscussionThread] {
        guard let expected = HnDiscussionURLCollector.normalize(sourceURL),
              let payload = try? JSONDecoder().decode(AlgoliaSearchResponse.self, from: data)
        else {
            return []
        }
        return payload.hits.compactMap { $0.thread(matching: expected) }
    }
}

private struct AlgoliaSearchResponse: Decodable {
    let hits: [AlgoliaHit]
}

private struct AlgoliaHit: Decodable {
    let objectID: String?
    let title: String?
    let url: String?
    let points: Int?
    let numComments: Int?

    enum CodingKeys: String, CodingKey {
        case objectID, title, url, points
        case numComments = "num_comments"
    }

    func thread(matching expectedNormalizedURL: String) -> HnDiscussionThread? {
        guard let objectID, !objectID.isEmpty,
              let title, !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let url, HnDiscussionURLCollector.normalize(url) == expectedNormalizedURL,
              let itemURL = URL(string: "https://news.ycombinator.com/item?id=\(objectID)")
        else {
            return nil
        }
        return HnDiscussionThread(
            objectID: objectID,
            title: title.trimmingCharacters(in: .whitespacesAndNewlines),
            score: points ?? 0,
            commentCount: numComments ?? 0,
            itemURL: itemURL
        )
    }
}
