import Foundation
import VouchaAPI
import VouchaModels

struct NativeChatProfileBioFact: Equatable {
    let profileId: String
    let markdown: String
}

struct NativeChatProfileLinkFact: Equatable, Identifiable {
    let id: String
    let linkType: ProfileLinkType
    let handle: String?
    let name: String?
    let url: String?

    var safeDestination: URL? {
        let nonblankHandle = handle.flatMap { value in
            value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : value
        }
        guard let destination = linkType.resolvedURL(handle: nonblankHandle, fallbackURL: url),
              destination.scheme?.lowercased() == "https",
              let host = destination.host, !host.isEmpty,
              destination.user == nil, destination.password == nil else { return nil }
        return destination
    }
}

extension NativeChatMCPFact {
    static func makeProfileBio(_ value: DecodedJSONValue) -> Self? {
        guard let data = try? value.exactJSONData(),
              let envelope = try? JSONDecoder.profileFact.decode(ProfileBioEnvelope.self, from: data),
              envelope.success,
              !envelope.profile.markdown.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        return .profileBio(.init(profileId: envelope.profile.id, markdown: envelope.profile.markdown))
    }

    static func makeProfileLinks(_ value: DecodedJSONValue) -> Self? {
        guard let data = try? value.exactJSONData(),
              let envelope = try? JSONDecoder.profileFact.decode(ProfileLinksEnvelope.self, from: data),
              envelope.success else { return nil }
        let links = envelope.results.filter { link in
            [link.name, link.handle, link.url].contains { value in
                value?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false
            }
        }
        guard !links.isEmpty else { return nil }
        return .profileLinks(links.map { link in
            .init(id: link.id, linkType: link.linkType, handle: link.handle, name: link.name, url: link.url)
        })
    }
}

private struct ProfileBioEnvelope: Decodable {
    let success: Bool
    let profile: ProfileBio

    struct ProfileBio: Decodable {
        let id: String
        let markdown: String
    }
}

private struct ProfileLinksEnvelope: Decodable {
    let success: Bool
    let results: [ProfileLinkSource]
}

private struct ProfileLinkSource: Decodable {
    let id: String
    let linkType: ProfileLinkType
    let handle: String?
    let name: String?
    let url: String?
}

private extension JSONDecoder {
    static let profileFact: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }()
}
