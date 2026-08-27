import Foundation
@testable import VouchaAPI

private struct AnyEncodable: Encodable {
    let value: any Encodable

    func encode(to encoder: Encoder) throws {
        try value.encode(to: encoder)
    }
}

func requestBodyJSON(from endpoint: Endpoint) throws -> JSONValue? {
    guard let body = endpoint.body else {
        return nil
    }

    let encoder = JSONEncoder()
    if endpoint.bodyKeyEncodingStrategy == .convertToSnakeCase {
        encoder.keyEncodingStrategy = .convertToSnakeCase
    }
    let data = try encoder.encode(AnyEncodable(value: body))
    return try JSONDecoder().decode(JSONValue.self, from: data)
}
