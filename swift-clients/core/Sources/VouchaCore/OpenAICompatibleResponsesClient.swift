import Foundation
#if canImport(CFNetwork)
    import CFNetwork
#endif
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

public struct OpenAICompatibleResponsesClient: Sendable {
    private static let maximumInputCharacters = 12_000
    private let session: URLSession
    private let addressResolver: any LocalLLMAddressResolving
    private let connectProbe: any LocalLLMConnectProbing

    public init(protocolClasses: [AnyClass]? = nil) {
        self.init(
            protocolClasses: protocolClasses,
            addressResolver: SystemLocalLLMAddressResolver(),
            connectProbe: SystemLocalLLMConnectProbe()
        )
    }

    init(
        protocolClasses: [AnyClass]? = nil,
        addressResolver: any LocalLLMAddressResolving,
        connectProbe: any LocalLLMConnectProbing = SystemLocalLLMConnectProbe()
    ) {
        let configuration = Self.makeSessionConfiguration(protocolClasses: protocolClasses)
        session = URLSession(
            configuration: configuration,
            delegate: LocalLLMURLSessionDelegate(),
            delegateQueue: nil
        )
        self.addressResolver = addressResolver
        self.connectProbe = connectProbe
    }

    static func makeSessionConfiguration(
        protocolClasses: [AnyClass]?
    ) -> URLSessionConfiguration {
        let configuration = URLSessionConfiguration.default
        configuration.timeoutIntervalForRequest = 600
        configuration.httpShouldSetCookies = false
        if let protocolClasses {
            configuration.protocolClasses = protocolClasses
        }
        disableHTTPProxy(on: configuration)
        return configuration
    }

    static func disableHTTPProxy(on configuration: URLSessionConfiguration) {
        #if canImport(Darwin)
            var settings: [String: Any] = [:]
            if let unmanaged = CFNetworkCopySystemProxySettings() {
                settings = (unmanaged.takeRetainedValue() as NSDictionary) as? [String: Any] ?? [:]
            }
            settings[kCFNetworkProxiesHTTPEnable as String] = false
            configuration.connectionProxyDictionary = settings
        #endif
    }

    public func generateAssistantResponse(
        message: String,
        history: [LocalLLMChatMessage],
        configuration: LocalLLMConfiguration,
        apiKey: String?
    ) async throws -> String? {
        guard let endpoint = configuration.selectedEndpoint else {
            throw LocalLLMError.invalidEndpoint
        }
        return try await generateAssistantResponse(
            message: message,
            history: history,
            endpoint: endpoint,
            apiKey: apiKey
        )
    }

    public func generateAssistantResponse(
        message: String,
        history: [LocalLLMChatMessage],
        endpoint: LocalLLMEndpointProfile,
        apiKey: String?
    ) async throws -> String? {
        guard let url = endpoint.responsesURL else {
            throw LocalLLMError.invalidEndpoint
        }
        guard let model = endpoint.selectedModel else {
            throw LocalLLMError.missingModel
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if let apiKey, !apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        }
        let input = Self.boundedInput(message: message, history: history)
        request.httpBody = try JSONEncoder().encode(ResponseRequest(model: model, input: input))

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await LocalLLMConnectionPinning.data(
                for: request,
                session: session,
                resolver: addressResolver,
                probe: connectProbe
            )
        } catch let error as URLError where error.code == .cancelled {
            if Task.isCancelled {
                throw CancellationError()
            }
            throw LocalLLMError.redirectNotAllowed
        }
        guard let http = response as? HTTPURLResponse else {
            throw LocalLLMError.invalidResponse
        }
        guard (200 ... 299).contains(http.statusCode) else {
            throw LocalLLMError.requestFailed(statusCode: http.statusCode)
        }
        let payload = try JSONDecoder().decode(ResponsePayload.self, from: data)
        return payload.outputText
    }

    private static func boundedInput(
        message: String,
        history: [LocalLLMChatMessage]
    ) -> [ResponseInput] {
        let latest = String(message.prefix(maximumInputCharacters))
        var remaining = maximumInputCharacters - latest.count
        var selectedHistory: [ResponseInput] = []
        for item in history.suffix(12).reversed() {
            guard remaining > 0 else { break }
            let content = String(item.content.prefix(remaining))
            guard !content.isEmpty else { continue }
            selectedHistory.append(ResponseInput(role: item.role, content: content))
            remaining -= content.count
        }
        return selectedHistory.reversed() + [ResponseInput(role: "user", content: latest)]
    }
}

private struct ResponseRequest: Encodable {
    let model: String
    let input: [ResponseInput]
}

private struct ResponseInput: Encodable {
    let role: String
    let content: String
}

private struct ResponsePayload: Decodable {
    let outputText: String?

    enum CodingKeys: String, CodingKey {
        case outputText = "output_text"
        case output
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        if let outputText = try container.decodeIfPresent(String.self, forKey: .outputText) {
            let trimmedOutputText = outputText.trimmingCharacters(in: .whitespacesAndNewlines)
            self.outputText = trimmedOutputText.isEmpty ? nil : trimmedOutputText
            return
        }
        let output = try container.decodeIfPresent([ResponseOutput].self, forKey: .output) ?? []
        let text = output
            .flatMap { $0.content ?? [] }
            .compactMap(\.text)
            .joined()
            .trimmingCharacters(in: .whitespacesAndNewlines)
        outputText = text.isEmpty ? nil : text
    }
}

private struct ResponseOutput: Decodable {
    let content: [ResponseContent]?
}

private struct ResponseContent: Decodable {
    let text: String?
}

final class LocalLLMURLSessionDelegate: NSObject, URLSessionTaskDelegate {
    func urlSession(
        _: URLSession,
        task _: URLSessionTask,
        willPerformHTTPRedirection _: HTTPURLResponse,
        newRequest _: URLRequest,
        completionHandler: @escaping (URLRequest?) -> Void
    ) {
        completionHandler(nil)
    }
}
