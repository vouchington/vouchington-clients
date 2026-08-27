import Foundation
import VouchaLocalization
import VouchaModels

public enum FediverseProviderFilter: String, CaseIterable, Sendable {
    case all
    case peertube
    case mastodon
    case lemmy
    case bluesky

    init(routeValue: String?) {
        self = routeValue.flatMap(Self.init(rawValue:)) ?? .all
    }

    var requestProviders: [String]? {
        self == .all ? nil : [rawValue]
    }

    var title: UiVerbatimText {
        switch self {
        case .all: .message(.nativeSwiftRouteSurfaceAllProviders)
        case .peertube: .externalProvider("PeerTube")
        case .mastodon: .externalProvider("Mastodon")
        case .lemmy: .externalProvider("Lemmy")
        case .bluesky: .externalProvider("Bluesky")
        }
    }

    var displayName: String {
        switch self {
        case .all: "Fediverse"
        case .peertube: "PeerTube"
        case .mastodon: "Mastodon"
        case .lemmy: "Lemmy"
        case .bluesky: "Bluesky"
        }
    }

    static func provider(_ rawValue: String) -> Self? {
        Self(rawValue: rawValue.lowercased())
    }
}

extension NativeRouteSurfaceViewModel {
    func groupedFediverseSections(from response: FediverseSearchResponse) -> [NativeSearchSection] {
        response.buckets.compactMap { bucket in
            let provider = FediverseProviderFilter.provider(bucket.provider)
            let providerName = provider?.displayName ?? FediverseProviderFilter.all.displayName
            let rows: [NativeRouteDestinationRow]
            if bucket.items.isEmpty {
                guard bucket.status != "ok" else { return nil }
                rows = [
                    NativeRouteDestinationRow(
                        icon: "exclamationmark.triangle",
                        title: .message(
                            .nativeSwiftRouteSurfaceProviderUnavailable,
                            parameters: ["provider": providerName]
                        ),
                        detail: .message(
                            .nativeSwiftRouteSurfaceProviderUnavailable,
                            parameters: ["provider": providerName]
                        )
                    )
                ]
            } else {
                rows = bucket.items.map {
                    NativeRouteDestinationRow(
                        icon: fediverseResultIcon($0.resultType),
                        title: .verbatim($0.title),
                        detail: fediverseResultDetail($0),
                        externalURL: safeHTTPURL($0.externalUrl)
                    )
                } + (bucket.status == "ok" ? [] : [NativeRouteDestinationRow(
                    icon: "exclamationmark.triangle",
                    title: .message(.nativeSwiftRouteSurfaceSomeResultsUnavailable),
                    detail: .message(.nativeSwiftRouteSurfaceSomeResultsUnavailable)
                )])
            }
            return NativeSearchSection(title: provider?.title ?? .externalProvider(providerName), rows: rows)
        }
    }

    private func safeHTTPURL(_ rawValue: String) -> URL? {
        guard let url = URL(string: rawValue), url.scheme == "http" || url.scheme == "https" else {
            return nil
        }
        return url
    }

    var fediverseProviders: [String]? {
        fediverseProvider.requestProviders
    }

    func fediverseRoute(query: String) -> String {
        var components = URLComponents()
        components.path = "/fediverse"
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        components.queryItems = [
            trimmed.isEmpty ? nil : URLQueryItem(name: "q", value: trimmed),
            fediverseProvider == .all ? nil : URLQueryItem(name: "provider", value: fediverseProvider.rawValue)
        ].compactMap { $0 }
        return components.string ?? "/fediverse"
    }

    private func fediverseResultIcon(_ resultType: String) -> String {
        switch resultType {
        case "video": "play.rectangle"
        case "profile": "person.crop.circle"
        default: "text.bubble"
        }
    }

    private func fediverseResultDetail(_ result: FediverseSearchResult) -> UiVerbatimText {
        let sourceHostname = result.sourceHostname
            ?? FediverseProviderFilter.provider(result.provider)?.displayName
            ?? FediverseProviderFilter.all.displayName
        if let authorName = result.authorName, !authorName.isEmpty {
            return .message(
                .nativeSwiftRouteSurfaceAuthorOnSource,
                parameters: ["author": authorName, "source": sourceHostname]
            )
        }
        return .verbatim(sourceHostname)
    }
}
