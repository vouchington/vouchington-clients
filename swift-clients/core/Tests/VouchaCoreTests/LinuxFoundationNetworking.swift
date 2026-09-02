import Foundation
#if !canImport(Darwin)
    @_exported import FoundationNetworking
#endif

enum IsolatedHTTPCookieStorage {
    static func make() -> HTTPCookieStorage {
        #if canImport(Darwin)
            HTTPCookieStorage()
        #else
            HTTPCookieStorage.sharedCookieStorage(forGroupContainerIdentifier: UUID().uuidString)
        #endif
    }
}

#if !canImport(Darwin)
    enum XCTContext {
        struct Activity {}

        @discardableResult
        static func runActivity<Result>(
            named _: String,
            block: (Activity) throws -> Result
        ) rethrows -> Result {
            try block(Activity())
        }
    }
#endif
