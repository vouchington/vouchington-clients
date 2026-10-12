import Foundation

public extension Endpoint {
    static func myEmailAddresses(after: String? = nil, limit: Int = 25) -> Endpoint {
        pagedGet(path: "/api/v1/my/email-addresses", after: after, limit: limit)
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

private struct EmailAddressVerificationRequestBody: Encodable {
    let emailAddress: String
}

private struct EmailAddressVerificationBody: Encodable {
    let token: String
}
