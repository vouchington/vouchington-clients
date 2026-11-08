import Foundation

public extension Endpoint {
    static func myEmailAddresses(after: String? = nil, limit: Int = 25) -> Endpoint {
        pagedGet(path: "/api/v1/my/email-addresses", after: after, limit: limit)
    }

    static func requestMyEmailAddressVerification(emailAddress: String, reAuthToken: String? = nil) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/email-addresses",
            body: EmailAddressVerificationRequestBody(emailAddress: emailAddress, reAuthToken: reAuthToken)
        )
    }

    static func verifyMyEmailAddress(emailAddress: String, token: String, reAuthToken: String? = nil) -> Endpoint {
        let encodedEmailAddress = pathSegment(emailAddress)
            .replacingOccurrences(of: "+", with: "%2B")
            .replacingOccurrences(of: "@", with: "%40")
        return Endpoint(
            .POST,
            path: "/api/v1/my/email-addresses/\(encodedEmailAddress)/verifications",
            body: EmailAddressVerificationBody(token: token, reAuthToken: reAuthToken)
        )
    }
}

private struct EmailAddressVerificationRequestBody: Encodable {
    let emailAddress: String
    let reAuthToken: String?
}

private struct EmailAddressVerificationBody: Encodable {
    let token: String
    let reAuthToken: String?
}
