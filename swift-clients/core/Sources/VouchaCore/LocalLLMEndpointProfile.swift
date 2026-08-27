import Foundation

public struct LocalLLMEndpointProfile: Codable, Equatable, Identifiable, Sendable {
    public let id: UUID
    public var displayName: String
    public var isEnabled: Bool
    public var endpoint: String
    public var modelNames: [String]
    public var selectedModelName: String

    public init(
        id: UUID = UUID(),
        displayName: String = "",
        isEnabled: Bool = false,
        endpoint: String = "",
        modelNames: [String] = [],
        selectedModelName: String = ""
    ) {
        self.id = id
        self.displayName = displayName
        self.isEnabled = isEnabled
        self.endpoint = endpoint
        self.modelNames = modelNames
        self.selectedModelName = selectedModelName
    }

    public var normalizedModelNames: [String] {
        var seen = Set<String>()
        return modelNames.compactMap { raw in
            let value = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !value.isEmpty, seen.insert(value).inserted else { return nil }
            return value
        }
    }

    public var selectedModel: String? {
        let value = selectedModelName.trimmingCharacters(in: .whitespacesAndNewlines)
        return value.isEmpty ? nil : value
    }

    public var responsesURL: URL? {
        Self.responsesURL(from: endpoint)
    }

    public var origin: String? {
        guard let url = responsesURL,
              let scheme = url.scheme?.lowercased(),
              let host = url.host?.lowercased()
        else { return nil }
        let port = url.port ?? (scheme == "https" ? 443 : 80)
        let hostComponent = host.contains(":") ? "[\(host)]" : host
        return "\(scheme)://\(hostComponent):\(port)"
    }

    public var isSelectableAsCurrent: Bool {
        isEnabled && responsesURL != nil && selectedModel != nil
    }

    public var credentialLoadIdentity: String {
        "\(id.uuidString)\u{1e}\(origin ?? "")"
    }

    public static func responsesURL(from endpoint: String) -> URL? {
        let trimmed = endpoint.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, var components = URLComponents(string: trimmed) else { return nil }
        let scheme = components.scheme?.lowercased()
        guard scheme == "http" || scheme == "https",
              components.user == nil,
              components.password == nil,
              components.query == nil,
              components.fragment == nil,
              let host = components.host?.lowercased(),
              !host.isEmpty,
              // A zone ID is rejected unconditionally, regardless of scheme: it is only
              // meaningful on the interface that assigned it, so an endpoint string containing
              // one can never be verified as portable/safe. Rejecting it here (rather than only
              // under the http-only cleartext gate below) also keeps `origin` canonicalization
              // from diverging between platforms — .NET's Uri.Host silently drops a zone ID
              // while Swift's URL.host retains it percent-decoded, so letting a zone-ID host
              // through under https would make the two clients disagree on the credential-scoping
              // origin for the same endpoint.
              !host.contains("%")
        else { return nil }
        if scheme == "http", !isAllowedCleartextHost(host) {
            return nil
        }
        components.scheme = scheme
        let path = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        if path.isEmpty {
            components.path = "/v1/responses"
        } else if path == "v1" {
            components.path = "/v1/responses"
        } else if !path.hasSuffix("responses") {
            components.path = "/" + path + "/responses"
        }
        return components.url
    }

    private static func isAllowedCleartextHost(_ host: String) -> Bool {
        let host = host.trimmingCharacters(in: CharacterSet(charactersIn: "[]"))

        if host == "::1" {
            return true
        }

        if host.contains(":") {
            guard let firstHextet = host.split(separator: ":", omittingEmptySubsequences: true).first,
                  let value = UInt16(firstHextet, radix: 16)
            else {
                return false
            }
            return (value & 0xFE00) == 0xFC00 || (value & 0xFFC0) == 0xFE80
        }

        let labels = host.split(separator: ".", omittingEmptySubsequences: false)
        if labels.count == 4,
           labels.allSatisfy({ $0.allSatisfy(\.isNumber) && UInt8($0) != nil }) {
            let octets = labels.compactMap { UInt8($0) }
            let isPrivate = octets[0] == 10
                || (octets[0] == 172 && (16 ... 31).contains(octets[1]))
                || (octets[0] == 192 && octets[1] == 168)
            let isLinkLocal = octets[0] == 169 && octets[1] == 254
            let isLoopback = octets[0] == 127
            return isPrivate || isLinkLocal || isLoopback
        }
        return labels.count == 1 || host.hasSuffix(".local")
    }
}

public struct LocalLLMConfiguration: Codable, Equatable, Sendable {
    public var isEnabled: Bool
    public var endpoints: [LocalLLMEndpointProfile]
    public var selectedEndpointID: UUID?
    public var selectedProviderID: String?

    public init(
        isEnabled: Bool = false,
        endpoints: [LocalLLMEndpointProfile] = [],
        selectedEndpointID: UUID? = nil,
        selectedProviderID: String? = nil
    ) {
        self.isEnabled = isEnabled
        self.endpoints = endpoints
        self.selectedEndpointID = selectedEndpointID
        self.selectedProviderID = selectedProviderID
    }

    public static let disabled = LocalLLMConfiguration()

    public var selectedEndpoint: LocalLLMEndpointProfile? {
        guard let selectedEndpointID else { return nil }
        return endpoint(id: selectedEndpointID)
    }

    public func endpoint(id: UUID) -> LocalLLMEndpointProfile? {
        endpoints.first { $0.id == id }
    }

}
