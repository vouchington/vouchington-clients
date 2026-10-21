import Foundation
import VouchaAPI
import VouchaModels

enum NativeChatMCPFact: Equatable {
    case emailPreferences(EmailPreferences)
    case generalPreferences(GeneralPreferences)
    case profileBio(NativeChatProfileBioFact)
    case profileLinks([NativeChatProfileLinkFact])
    case feedPostIds([String])

    static func makeGeneralPreferences(_ value: DecodedJSONValue) -> Self? {
        guard let data = try? value.exactJSONData(),
              let envelope = try? JSONDecoder.preferenceFact.decode(GeneralPreferencesEnvelope.self, from: data),
              envelope.success else { return nil }
        return .generalPreferences(envelope.settings)
    }

    static func makeEmailPreferences(_ value: DecodedJSONValue) -> Self? {
        guard let data = try? value.exactJSONData(),
              let envelope = try? JSONDecoder.preferenceFact.decode(EmailPreferencesEnvelope.self, from: data),
              envelope.success, valid(envelope.emailPreferences) else { return nil }
        return .emailPreferences(envelope.emailPreferences)
    }

    private static func valid(_ preferences: EmailPreferences) -> Bool {
        let frequencies = ["none", "daily", "weekly"]
        let cadences = ["daily", "selected_days", "weekly"]
        return frequencies.contains(preferences.newsDigestFrequency) &&
            frequencies.contains(preferences.communityDigestFrequency) &&
            cadences.contains(preferences.moderationEmailCadence) &&
            preferences.moderationEmailDaysOfWeek.allSatisfy { (1 ... 7).contains($0) } &&
            Set(preferences.moderationEmailDaysOfWeek).count == preferences.moderationEmailDaysOfWeek.count &&
            !preferences.moderationEmailTimeOfDay.isEmpty
    }
}

private struct EmailPreferencesEnvelope: Decodable {
    let success: Bool
    let emailPreferences: EmailPreferences
}

private struct GeneralPreferencesEnvelope: Decodable {
    let success: Bool
    let settings: GeneralPreferences
}

private extension JSONDecoder {
    static let preferenceFact: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }()
}
