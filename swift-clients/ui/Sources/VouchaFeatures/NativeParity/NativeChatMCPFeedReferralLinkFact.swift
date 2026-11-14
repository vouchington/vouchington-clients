import Foundation
import VouchaAPI
import VouchaModels

extension NativeChatMCPFact {
    /// Referral feed rows identify a program and URL. They are not hydrated cards.
    static func makeFeedReferralLinks(_ value: DecodedJSONValue) -> Self? {
        guard let data = try? value.exactJSONData(),
              let envelope = try? JSONDecoder.referralLinks.decode(FeedReferralLinksEnvelope.self, from: data),
              envelope.success,
              envelope.results.count <= 100,
              let links = NativeChatFeedReferralLinkFact.make(envelope.results) else { return nil }
        return .feedReferralLinks(links)
    }
}

extension NativeChatFeedReferralLinkFact {
    fileprivate static func make(_ rows: [FeedReferralLinkRow]) -> [Self]? {
        var seen: Set<String> = []
        var links: [Self] = []
        for row in rows {
            guard let link = validated(row), seen.insert(link.id).inserted else { return nil }
            links.append(link)
        }
        return links
    }

    private static func validated(_ row: FeedReferralLinkRow) -> Self? {
        let programName = row.referralProgramName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !row.id.isEmpty, !row.userId.isEmpty, !row.referralProgramId.isEmpty,
              !row.referralProgramSlug.isEmpty, !programName.isEmpty,
              acceptableURL(row.url) else { return nil }
        return Self(id: row.id, programName: programName, url: row.url)
    }

    private static func acceptableURL(_ value: String) -> Bool {
        guard let components = URLComponents(string: value),
              let scheme = components.scheme?.lowercased(),
              scheme == "https" || scheme == "http",
              let host = components.host, !host.isEmpty,
              components.user == nil, components.password == nil else { return false }
        return true
    }
}

private struct FeedReferralLinksEnvelope: Decodable {
    let success: Bool
    let results: [FeedReferralLinkRow]
}

private struct FeedReferralLinkRow: Decodable {
    let id: String
    let userId: String
    let referralProgramId: String
    let referralProgramName: String
    let referralProgramSlug: String
    let url: String

    private enum CodingKeys: String, CodingKey {
        case id, userId, referralProgramId, referralProgramName, referralProgramSlug, url, label
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        userId = try container.decode(String.self, forKey: .userId)
        referralProgramId = try container.decode(String.self, forKey: .referralProgramId)
        referralProgramName = try container.decode(String.self, forKey: .referralProgramName)
        referralProgramSlug = try container.decode(String.self, forKey: .referralProgramSlug)
        url = try container.decode(String.self, forKey: .url)
        guard container.contains(.label) else {
            throw DecodingError.keyNotFound(
                CodingKeys.label,
                .init(codingPath: decoder.codingPath, debugDescription: "")
            )
        }
        _ = try container.decodeIfPresent(String.self, forKey: .label)
    }
}

private extension JSONDecoder {
    static let referralLinks: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }()
}
