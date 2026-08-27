import Foundation

public extension Endpoint {
    static func myEmailAddresses(after: String? = nil, limit: Int = 25) -> Endpoint {
        paginatedGet(path: "/api/v1/my/email-addresses", after: after, limit: limit)
    }

    static func requestMyEmailAddressVerification(emailAddress: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/email-addresses",
            body: EmailAddressVerificationRequestBody(emailAddress: emailAddress)
        )
    }

    static func verifyMyEmailAddress(emailAddress: String, token: String) -> Endpoint {
        let encodedEmailAddress = pathSegment(emailAddress)
            .replacingOccurrences(of: "+", with: "%2B")
            .replacingOccurrences(of: "@", with: "%40")
        return Endpoint(
            .POST,
            path: "/api/v1/my/email-addresses/\(encodedEmailAddress)/verifications",
            body: EmailAddressVerificationBody(token: token)
        )
    }
}

private func paginatedGet(path: String, after: String?, limit: Int) -> Endpoint {
    var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
    if let after {
        queryItems.append(URLQueryItem(name: "after", value: after))
    }
    return Endpoint(.GET, path: path, queryItems: queryItems)
}

private struct EmailAddressVerificationRequestBody: Encodable {
    let emailAddress: String
}

private struct EmailAddressVerificationBody: Encodable {
    let token: String
}
