import Foundation
@testable import VouchaCore
import XCTest

func makeLocalLLMConfiguration(
    isEnabled: Bool = true,
    endpoint: String = "http://localhost:2999/v1",
    modelNames: [String] = ["gpt-oss"],
    selectedModelName: String = "gpt-oss",
    endpointID: UUID = UUID()
) -> LocalLLMConfiguration {
    let profile = LocalLLMEndpointProfile(
        id: endpointID,
        isEnabled: isEnabled,
        endpoint: endpoint,
        modelNames: modelNames,
        selectedModelName: selectedModelName
    )
    return LocalLLMConfiguration(
        isEnabled: isEnabled,
        endpoints: [profile],
        selectedEndpointID: endpointID
    )
}

final class LocalLLMTestURLProtocol: URLProtocol {
    static var responseData = Data(#"{"output_text":"Local answer"}"#.utf8)
    static var statusCode = 200
    static var capturedRequest: URLRequest?
    static var capturedBody: Data?

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.capturedRequest = request
        Self.capturedBody = request.httpBody ?? request.httpBodyStream.flatMap(Self.readBodyStream)
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: Self.statusCode,
            httpVersion: nil,
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Self.responseData)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    static func reset(responseData: Data = Data(#"{"output_text":"Local answer"}"#.utf8), statusCode: Int = 200) {
        self.responseData = responseData
        self.statusCode = statusCode
        capturedRequest = nil
        capturedBody = nil
    }

    private static func readBodyStream(_ stream: InputStream) -> Data {
        stream.open()
        defer { stream.close() }
        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 4_096)
        while stream.hasBytesAvailable {
            let count = stream.read(&buffer, maxLength: buffer.count)
            if count <= 0 {
                break
            }
            data.append(buffer, count: count)
        }
        return data
    }
}

func makeLocalLLMSettingsStore(
    configuration: LocalLLMConfiguration = .disabled,
    apiKey: String? = nil
) async -> LocalLLMSettingsStore {
    let directory = FileManager.default.temporaryDirectory
        .appendingPathComponent(UUID().uuidString, isDirectory: true)
    let store = LocalLLMSettingsStore(
        fileURL: directory.appendingPathComponent("local-llm-settings.json"),
        secretStore: InMemoryLocalLLMSecretStore()
    )
    let didSave: Bool = if let endpointID = configuration.selectedEndpointID {
        await store.save(configuration, apiKeys: [endpointID: apiKey ?? ""])
    } else {
        await store.save(configuration)
    }
    XCTAssertTrue(didSave)
    return store
}
