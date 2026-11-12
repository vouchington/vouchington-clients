import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

/// An opaque snapshot used only to reject account confirmation after a cookie context change.
public struct AccountConfirmationContext: Equatable, Sendable {
    let device: String
    let session: String
}

public extension APIClient {
    func accountConfirmationContext() -> AccountConfirmationContext? {
        let cookies = cookieStorage.cookies(for: baseURL) ?? []
        guard let device = cookies.first(where: { $0.name == "dt" })?.value,
              let session = cookies.first(where: { $0.name == "st" })?.value,
              !device.isEmpty, !session.isEmpty else { return nil }
        return AccountConfirmationContext(device: device, session: session)
    }
}

public enum AccountConfirmationError: Error, Sendable {
    case invalidProof
    case contextChanged
    case unavailableMethod
}
