public struct ClientMetadata: Sendable, Equatable {
    public enum Platform: String, Sendable {
        case ios
        case ipados
        case macos
        case android
    }

    public let platform: Platform
    public let appVersion: String
    public let sdkVersion: String?

    public init(platform: Platform, appVersion: String, sdkVersion: String? = nil) {
        self.platform = platform
        self.appVersion = appVersion
        self.sdkVersion = sdkVersion
    }
}
