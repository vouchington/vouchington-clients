import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    public func search(query: String) async {
        guard let client, destination == .webSearch || destination == .fediverseSearch else { return }
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        searchGeneration += 1
        let generation = searchGeneration
        guard !trimmed.isEmpty else {
            searchSections = []
            rows = destination?.nativeRows ?? []
            state = .idle
            return
        }

        state = .loading
        do {
            let sections: [NativeSearchSection]
            if destination == .fediverseSearch {
                let response: FediverseSearchResponse = try await client.send(.fediverseSearch(
                    query: trimmed,
                    providers: fediverseProviders,
                    limit: 10
                ))
                sections = groupedFediverseSections(from: response)
            } else {
                let response: OmnisearchResponse = try await client.send(.combinedSearch(query: trimmed, limit: 10))
                sections = groupedSearchSections(from: response)
            }
            guard generation == searchGeneration, !Task.isCancelled else { return }
            searchSections = sections
            rows = searchSections.flatMap(\.rows)
            state = .loaded
        } catch let error as VouchaError {
            guard generation == searchGeneration, !Task.isCancelled else { return }
            searchSections = []
            state = .error(error)
        } catch {
            guard generation == searchGeneration, !Task.isCancelled else { return }
            searchSections = []
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func groupedSearchSections(from response: OmnisearchResponse) -> [NativeSearchSection] {
        [
            topicsSection(from: response),
            postsSection(from: response),
            newsSection(from: response),
            domainsSection(from: response),
            communitiesSection(from: response)
        ].compactMap { $0 }
    }

    private func topicsSection(from response: OmnisearchResponse) -> NativeSearchSection? {
        guard !response.topics.isEmpty else { return nil }
        return NativeSearchSection(
            title: .message(.nativeSwiftRouteSurfaceTopics),
            rows: response.topics.map {
                NativeRouteDestinationRow(
                    icon: "tag",
                    title: .message(.nativeSwiftRouteSurfaceTopicValue, parameters: ["value": $0.name]),
                    detail: .verbatim($0.topicType)
                )
            }
        )
    }

    private func postsSection(from response: OmnisearchResponse) -> NativeSearchSection? {
        guard !response.posts.isEmpty else { return nil }
        return NativeSearchSection(
            title: .message(.nativeSwiftRouteSurfacePosts),
            rows: response.posts.map { post in
                let authoredTitle = post.authoredTitle?.trimmingCharacters(in: .whitespacesAndNewlines)
                let title = authoredTitle.flatMap { $0.isEmpty ? nil : $0 }
                return NativeRouteDestinationRow(
                    icon: "doc.text",
                    title: .verbatim(title ?? post.title),
                    detail: humanizedPostType(post.postType),
                    declaredLanguage: title == nil ? nil : post.declaredLanguage,
                    detectedLanguage: title == nil ? nil : post.linguaRsDetectedLanguage
                )
            }
        )
    }

    private func newsSection(from response: OmnisearchResponse) -> NativeSearchSection? {
        guard !response.news.isEmpty else { return nil }
        return NativeSearchSection(
            title: .message(.nativeSwiftRouteSurfaceNews),
            rows: response.news.map {
                NativeRouteDestinationRow(
                    icon: "newspaper",
                    title: .message(.nativeSwiftRouteSurfaceArticleValue, parameters: ["value": $0.title]),
                    detail: .verbatim($0.feedTitle)
                )
            }
        )
    }

    private func domainsSection(from response: OmnisearchResponse) -> NativeSearchSection? {
        guard !response.domains.isEmpty else { return nil }
        return NativeSearchSection(
            title: .message(.nativeSwiftRouteSurfaceDomains),
            rows: response.domains.map {
                NativeRouteDestinationRow(
                    icon: "globe",
                    title: UiMessage(.nativeSwiftRouteSurfaceDomainValue, parameters: ["value": $0.hostname]),
                    detail: UiMessage(.nativeSwiftRouteSurfaceDomain)
                )
            }
        )
    }

    private func communitiesSection(from response: OmnisearchResponse) -> NativeSearchSection? {
        guard !response.communities.isEmpty else { return nil }
        let orderedCommunities = response.communities.filter(\.bookmarked)
            + response.communities.filter { !$0.bookmarked }
        return NativeSearchSection(
            title: .message(.nativeSwiftRouteSurfaceCommunities),
            rows: orderedCommunities.map {
                NativeRouteDestinationRow(
                    icon: "person.3",
                    title: .message(.nativeSwiftRouteSurfaceCommunityValue, parameters: ["value": $0.name]),
                    detail: $0.bookmarked
                        ? .message(.nativeSwiftRouteSurfaceBookmarked)
                        : .verbatim($0.slug)
                )
            }
        )
    }

    private func humanizedPostType(_ postType: String) -> UiVerbatimText {
        switch postType {
        case "discussion": .message(.nativeSwiftRouteSurfaceDiscussion)
        case "review": .message(.nativeSwiftRouteSurfaceReview)
        case "article": .message(.nativeSwiftRouteSurfaceArticle)
        case "blog_post": .message(.nativeSwiftRouteSurfaceBlogPost)
        case "story": .message(.nativeSwiftRouteSurfaceStory)
        case "link": .message(.nativeSwiftRouteSurfaceLink)
        case "data_point": .message(.nativeSwiftRouteSurfaceDataPoint)
        case "comment": .message(.nativeSwiftRouteSurfaceComment)
        default: .verbatim(postType)
        }
    }

}
