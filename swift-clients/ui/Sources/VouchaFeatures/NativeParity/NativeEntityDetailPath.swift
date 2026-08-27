import Foundation

enum NativeEntityDetailPath {
    static func topic(id: String, slug: String?, topicType: String?) -> String {
        let type = topicType.flatMap { $0.isEmpty ? nil : $0 } ?? "topic"
        let identifier = type == "topic_recommendation" ? id : slug ?? id
        let segment = switch type {
        case "rss_feed", "source": "source"
        case "fediverse_instance": "instance"
        case "topic_recommendation": "topic-recommendations"
        default: type.replacingOccurrences(of: "_", with: "-")
        }
        return "/\(segment)/\(escaped(identifier))"
    }

    static func source(feedId: String, topicId: String?, topicSlug: String?) -> String {
        "/source/\(escaped(topicSlug ?? topicId ?? feedId))"
    }

    private static let pathSegmentCharacters: CharacterSet = {
        var characters = CharacterSet.urlPathAllowed
        characters.remove(charactersIn: "/:%")
        return characters
    }()

    private static func escaped(_ value: String) -> String {
        value.addingPercentEncoding(withAllowedCharacters: pathSegmentCharacters) ?? value
    }
}
