import Foundation
import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest

/// One canonical `mcp-results.json` case plus the arguments the model would send for it.
struct NativeChatMCPHTTPFixture {
    let tool: String
    let arguments: [String: Any]
    let structured: Any

    init(_ id: String) throws {
        let cases = try XCTUnwrap(Self.load("mcp-results.json")["cases"] as? [[String: Any]])
        let row = try XCTUnwrap(cases.first { $0["id"] as? String == id }, id)
        tool = try XCTUnwrap(row["tool"] as? String)
        arguments = try XCTUnwrap(row["arguments"] as? [String: Any])
        structured = try XCTUnwrap(row["structuredContent"])
    }

    /// The fact the built-in providers publish for this exact result.
    var builtInFact: NativeChatMCPFact? {
        let option = (arguments["option"] as? String).map(DecodedJSONValue.string)
        guard let value = try? DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: structured))
        else { return nil }
        let nested = arguments["arguments"].flatMap {
            try? DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: $0))
        }
        return NativeChatMCPFact.make(tool: tool, option: option, arguments: nested, content: value)
    }

    /// A Responses API function-call turn with the provider-issued call identity.
    func call(_ callId: String) throws -> (Data, Int, TimeInterval) {
        let raw = try String(decoding: JSONSerialization.data(withJSONObject: arguments), as: UTF8.self)
        let item: [String: Any] = ["type": "function_call", "call_id": callId, "name": tool, "arguments": raw]
        return try (JSONSerialization.data(withJSONObject: ["output": [item]]), 200, 0)
    }

    static func load(_ file: String) throws -> [String: Any] {
        let root = try ContractRoot.url(requiredPaths: ["api-fixtures/v1/\(file)"])
        let url = try ContractRoot.requiredURL("api-fixtures/v1/\(file)", root: root)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
    }

    private static let legalFactTools: Set = [
        "search_copyright_notices_for_association",
        "list_repeat_infringer_accounts"
    ]

    /// The member server's tools plus the legal server's copyright tools. Native chat connects only to the member
    /// server today, so the legal tools exercise the shared bridge through a catalog a legal connection would supply.
    static func publishedCatalog() throws -> DecodedJSONValue {
        let servers = try XCTUnwrap(load("mcp.json")["servers"] as? [[String: Any]])
        let tools = try ["voucha-user-mcp", "voucha-legal-mcp"].flatMap { name -> [Any] in
            let server = try XCTUnwrap(servers.first { $0["name"] as? String == name })
            return try XCTUnwrap(server["tools"] as? [[String: Any]]).compactMap { $0["tool"] as? [String: Any] }
                .filter { name == "voucha-user-mcp" || Self.legalFactTools.contains($0["name"] as? String ?? "") }
        }
        return try DecodedJSONValue(exactJSONData: JSONSerialization.data(withJSONObject: ["tools": tools]))
    }
}

/// A view model bound to the real HTTP provider, a stubbed Responses endpoint and an MCP service.
@MainActor
struct NativeChatMCPHTTPHarness {
    let service = BridgeMCPService()
    let session: NativeChatMCPAgentSession
    let viewModel: NativeChatViewModel

    init(
        fixture: NativeChatMCPHTTPFixture, isError: Bool = false, structured: Any? = nil
    ) async throws {
        service.catalog = try NativeChatMCPHTTPFixture.publishedCatalog()
        service.toolResult = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: JSONSerialization.data(
            withJSONObject: ["content": [], "structuredContent": structured ?? fixture.structured, "isError": isError]
        ))
        session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let configuration = makeLocalLLMConfiguration()
        let endpointID = try XCTUnwrap(configuration.selectedEndpoint).id
        let provider = await NativeChatLocalTitleProvider(
            settingsStore: makeLocalLLMSettingsStore(configuration: configuration),
            featurePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            responsesClient: OpenAICompatibleResponsesClient(
                protocolClasses: [CannedFeedURLProtocol.self], addressResolver: HTTPFactResolver(),
                connectProbe: HTTPFactProbe()
            )
        )
        let kind = NativeChatTitleProviderKind.openAICompatible(endpointID: endpointID)
        viewModel = try NativeChatViewModel(
            client: APIClient(
                config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999")), turnstileSiteKey: "key"),
                cookieStorage: HTTPCookieStorage(), protocolClasses: [CannedFeedURLProtocol.self]
            ),
            routeMatch: nil,
            titleProviderResolver: StubTitleProviderResolver(
                defaultSelectionValue: kind, providers: [kind: provider]
            ),
            mcpSession: session, currentMemberId: "member-a"
        )
        viewModel.selectedConversationId = "conversation-1"
    }

    func send(text: String? = nil) async {
        if let text { viewModel.draftMessage = text } else if viewModel.draftMessage.isEmpty {
            viewModel.draftMessage = "Read my data"
        }
        await viewModel.sendDraftMessage()
    }
}

private struct HTTPFactResolver: LocalLLMAddressResolving {
    func addresses(for _: String) async throws -> [LocalLLMIPAddress] {
        try [XCTUnwrap(LocalLLMIPAddress.parse("127.0.0.1"))]
    }
}

private struct HTTPFactProbe: LocalLLMConnectProbing {
    func canConnect(to _: LocalLLMIPAddress, port _: Int, timeout _: TimeInterval) async throws -> Bool {
        true
    }
}
