import Foundation

public struct AppConfig: Sendable {
    public let baseURL: URL
    public let imageBaseURL: URL
    public let turnstileSiteKey: String
    public var requiredTurnstileSiteKey: String {
        guard !turnstileSiteKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            preconditionFailure("Missing VOUCHA_TURNSTILE_SITE_KEY")
        }
        return turnstileSiteKey
    }

    /// Named constants for production base URLs.
    /// Not used as default arguments (Swift restricts static member access in default arg expressions);
    /// the shared computed initializer and unit tests reference them directly.
    public static let defaultAPIBaseURL = "https://voucha.ai"
    static let defaultImageBaseURL = URL(string: "https://images.voucha.ai")!
    public static let shared: AppConfig = from(environment: ProcessInfo.processInfo.environment)

    public static func from(environment: [String: String]) -> AppConfig {
        let raw = read(environment, "VOUCHA_API_BASE_URL") ?? defaultAPIBaseURL
        guard let url = URL(string: raw) else {
            preconditionFailure("Invalid VOUCHA_API_BASE_URL: \(raw)")
        }
        let imageRaw = read(environment, "VOUCHA_IMAGE_BASE_URL") ?? defaultImageBaseURL.absoluteString
        guard let imageUrl = URL(string: imageRaw) else {
            preconditionFailure("Invalid VOUCHA_IMAGE_BASE_URL: \(imageRaw)")
        }
        let turnstileSiteKey =
            read(environment, "VOUCHA_TURNSTILE_SITE_KEY") ??
            read(environment, "NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY")
        return AppConfig(baseURL: url, imageBaseURL: imageUrl, turnstileSiteKey: turnstileSiteKey ?? "")
    }

    private static func read(_ environment: [String: String], _ key: String) -> String? {
        guard let value = environment[key]?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }
        return value
    }

    /// Creates an `AppConfig` with the given API base URL and optional image CDN base URL.
    /// The `imageBaseURL` default uses a literal because Swift does not allow static member
    /// references (e.g. `Self.defaultImageBaseURL`) in default argument expressions.
    public init(
        baseURL: URL,
        imageBaseURL: URL = URL(string: "https://images.voucha.ai")!,
        turnstileSiteKey: String = ""
    ) {
        self.baseURL = baseURL
        self.imageBaseURL = imageBaseURL
        self.turnstileSiteKey = turnstileSiteKey
    }

    /// Constructs an image CDN URL for the given image ID at the requested width.
    /// Returns `nil` when `imageId` is nil or empty.
    /// Uses URLComponents to ensure the image ID is properly percent-encoded.
    public func imageURL(forImageId imageId: String?, width: Int = 96) -> String? {
        guard let imageId, !imageId.isEmpty else { return nil }
        guard var components = imageComponents() else { return nil }
        let imagePath = imageId.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? imageId
        components.percentEncodedPath += "/images/\(imagePath)"
        components.queryItems = [URLQueryItem(name: "w", value: "\(width)")]
        return components.url?.absoluteString
    }

    /// Constructs a placement-bound image CDN URL for persisted media.
    public func imageURL(
        forPlacementId placementId: String?,
        revision: Int,
        imageId: String?,
        width: Int = 96
    ) -> String? {
        guard let placementId, !placementId.isEmpty,
              let imageId, !imageId.isEmpty
        else { return nil }
        guard var components = imageComponents() else { return nil }
        let path = "/images/placements/\(placementId.placementPathSegment)/\(revision)/\(imageId.placementPathSegment)"
        components.percentEncodedPath += path
        components.queryItems = [URLQueryItem(name: "w", value: "\(width)")]
        return components.url?.absoluteString
    }

    private func imageComponents() -> URLComponents? {
        guard var components = URLComponents(url: imageBaseURL, resolvingAgainstBaseURL: false) else { return nil }
        while components.percentEncodedPath.hasSuffix("/") {
            components.percentEncodedPath.removeLast()
        }
        return components
    }
}

private extension String {
    var placementPathSegment: String {
        var allowed = CharacterSet.urlPathAllowed
        allowed.remove(charactersIn: "/:")
        return addingPercentEncoding(withAllowedCharacters: allowed) ?? self
    }
}

public enum VouchaURLResolver {
    public static func absoluteString(for urlString: String?, relativeTo baseURL: URL) -> String? {
        guard let urlString, !urlString.isEmpty else { return nil }
        if urlString.hasPrefix("//"), let scheme = baseURL.scheme {
            return "\(scheme):\(urlString)"
        }
        if let url = URL(string: urlString), url.scheme != nil {
            return url.absoluteString
        }
        return URL(string: urlString, relativeTo: baseURL)?.absoluteURL.absoluteString
    }
}
