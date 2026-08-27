import Foundation

extension APIClient {
    func encodeBody(_ body: any Encodable, for endpoint: Endpoint) throws -> Data {
        switch endpoint.bodyKeyEncodingStrategy {
        case .convertToSnakeCase:
            try encoder.encode(body)
        case .useDefaultKeys:
            try Self.defaultKeyEncoder.encode(body)
        }
    }

    private static let defaultKeyEncoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }()
}
