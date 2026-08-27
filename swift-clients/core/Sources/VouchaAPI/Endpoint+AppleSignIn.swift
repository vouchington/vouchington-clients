/// Body for the Apple Sign In continue endpoint.
/// The backend expects `userData` in camelCase, so encode via dictionary keys that are not
/// transformed by `JSONEncoder.keyEncodingStrategy`.
struct AppleSignInBody: Encodable {
    let token: String
    let nonce: String?
    let userName: String?

    func encode(to encoder: any Encoder) throws {
        var payload: [String: AppleSignInJSONValue] = ["token": .string(token)]
        if let nonce {
            payload["nonce"] = .string(nonce)
        }
        if let userName {
            payload["userData"] = .object(["name": .string(userName)])
        }
        var container = encoder.singleValueContainer()
        try container.encode(payload)
    }
}

private enum AppleSignInJSONValue: Encodable {
    case string(String)
    case object([String: AppleSignInJSONValue])

    func encode(to encoder: any Encoder) throws {
        switch self {
        case let .string(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case let .object(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        }
    }
}

public extension Endpoint {
    /// Authenticate via Apple Sign In identity token.
    static func appleSignIn(token: String, nonce: String?, userName: String?) -> Endpoint {
        let body = AppleSignInBody(
            token: token,
            nonce: nonce,
            userName: userName
        )
        return Endpoint(.POST, path: "/api/v1/auth/oauth/apple/continue", body: body)
    }
}
