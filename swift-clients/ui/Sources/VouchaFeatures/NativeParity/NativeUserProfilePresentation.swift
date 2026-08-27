import Foundation
import VouchaModels

extension NativeUserProfileSurface {
    func resolvedProfileLinkURL(_ link: ProfileLink) -> URL? {
        link.linkType.resolvedURL(handle: link.handle, fallbackURL: link.url)
    }
}

extension ProfileLinkType {
    func resolvedURL(handle: String?, fallbackURL: String?) -> URL? {
        let handleURL = handle.flatMap { handle -> String? in
            guard !handle.isEmpty else { return nil }
            return switch self {
            case .twitter: "https://x.com/\(handle)"
            case .facebook: "https://facebook.com/\(handle)"
            case .instagram: "https://instagram.com/\(handle)"
            case .github: "https://github.com/\(handle)"
            case .linkedin: "https://linkedin.com/in/\(handle)"
            case .youtube: "https://youtube.com/@\(handle)"
            case .tiktok: "https://tiktok.com/@\(handle)"
            case .url: nil
            }
        }
        return (handleURL ?? fallbackURL).flatMap(URL.init(string:))
    }
}
