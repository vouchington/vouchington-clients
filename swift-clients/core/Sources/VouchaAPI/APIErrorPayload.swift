import Foundation
import VouchaCore

struct APIErrorPayload: Decodable {
    let code: String?
    let userErrorText: String?
    let message: String?
    let error: String?
    let detail: String?
    let title: String?
    let nestedErrorMessage: String?

    var displayMessage: String? {
        [
            userErrorText,
            message,
            error,
            nestedErrorMessage,
            detail,
            title
        ].compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first { !$0.isEmpty }
    }

    static func decode(from data: Data, logger: VouchaLogger) -> APIErrorPayload? {
        guard !data.isEmpty else { return nil }
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        do {
            return try decoder.decode(APIErrorPayload.self, from: data)
        } catch {
            logger.debug("Failed to decode API error payload: \(error.localizedDescription)")
            return nil
        }
    }

    private enum CodingKeys: String, CodingKey {
        case code
        case userErrorText
        case message
        case error
        case detail
        case title
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let nestedError = try? container.decode(APIErrorPayload.self, forKey: .error)
        code = container.decodeStringIfPresent(forKey: .code) ?? nestedError?.code
        userErrorText = container.decodeStringIfPresent(forKey: .userErrorText)
        message = container.decodeStringIfPresent(forKey: .message)
        error = container.decodeStringIfPresent(forKey: .error)
        detail = container.decodeStringIfPresent(forKey: .detail)
        title = container.decodeStringIfPresent(forKey: .title)
        nestedErrorMessage = nestedError?.displayMessage
    }
}

private extension KeyedDecodingContainer {
    func decodeStringIfPresent(forKey key: Key) -> String? {
        try? decodeIfPresent(String.self, forKey: key)
    }
}
