import Foundation
#if canImport(UIKit)
    import UIKit
#endif
import VouchaAPI

enum NativeClientMetadata {
    static func current(bundle: Bundle = .main) -> ClientMetadata {
        ClientMetadata(platform: platform, appVersion: appVersion(bundle: bundle))
    }

    private static var platform: ClientMetadata.Platform {
        #if os(Android)
            .android
        #elseif canImport(UIKit)
            UIDevice.current.userInterfaceIdiom == .pad ? .ipados : .ios
        #else
            .macos
        #endif
    }

    private static func appVersion(bundle: Bundle) -> String {
        let displayVersion = bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String
        let build = bundle.object(forInfoDictionaryKey: "CFBundleVersion") as? String
        return [displayVersion, build].compactMap { $0 }.joined(separator: "+")
    }
}
