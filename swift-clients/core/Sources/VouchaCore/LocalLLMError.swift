import Foundation

public enum LocalLLMError: Error, LocalizedError, Equatable {
    case invalidEndpoint
    case missingModel
    case invalidResponse
    case redirectNotAllowed
    case requestFailed(statusCode: Int)
    case resolvedAddressNotPrivate(host: String)
    case dnsResolutionFailed
    case noUsableResolvedAddress(host: String)
    case unableToConnect(host: String)

    public var errorDescription: String? {
        switch self {
        case .invalidEndpoint:
            "Enter a valid Responses API endpoint."
        case .missingModel:
            "Choose a local model."
        case .invalidResponse:
            "The local model returned an invalid response."
        case .redirectNotAllowed:
            "The local model endpoint redirected the request. Configure the final endpoint directly."
        case let .requestFailed(statusCode):
            "The local model request failed with status \(statusCode)."
        case let .resolvedAddressNotPrivate(host):
            "The local model endpoint '\(host)' resolved to an address that is not on a private or local network."
        case .dnsResolutionFailed:
            "The local model endpoint could not be resolved."
        case let .noUsableResolvedAddress(host):
            "The local model endpoint '\(host)' did not resolve to a usable address."
        case let .unableToConnect(host):
            "Unable to connect to any resolved address for the local model endpoint '\(host)'."
        }
    }
}
