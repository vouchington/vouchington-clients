import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class ViewModelTests: XCTestCase {
    func testSuccessfulGenerationUsesBoundedHistoryAndAppendsAssistant() async {
        let runtime = FakeAICoreRuntime(state: .available, response: "On-device answer")
        let viewModel = makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        viewModel.draft = "How should I redeem points?"

        await viewModel.sendOnDevice()

        XCTAssertEqual(viewModel.messages.map(\.role), ["user", "assistant"])
        XCTAssertEqual(viewModel.messages.last?.content, "On-device answer")
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 1)
        XCTAssertTrue(generation.lastPrompt?.contains("How should I redeem points?") == true)
    }

    func testGenerationFailureRestoresDraftWithoutAlternateProviderRequest() async {
        let runtime = FakeAICoreRuntime(state: .available, error: TestError.failed)
        let viewModel = makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        viewModel.draft = "Keep this draft"

        await viewModel.sendOnDevice()

        XCTAssertEqual(viewModel.draft, "Keep this draft")
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNotNil(viewModel.errorMessage)
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 1)
    }

    func testDownloadRequiresAnExplicitCall() async {
        let runtime = FakeAICoreRuntime(state: .downloadable, response: "")
        let viewModel = makeViewModel(runtime: runtime)

        await viewModel.refreshStatus()
        XCTAssertEqual(viewModel.modelState, .downloadable)
        let downloadCountBeforeConfirmation = await runtime.downloadCount()
        XCTAssertEqual(downloadCountBeforeConfirmation, 0)

        await viewModel.downloadModel()
        XCTAssertEqual(viewModel.modelState, .available)
        let downloadCountAfterConfirmation = await runtime.downloadCount()
        XCTAssertEqual(downloadCountAfterConfirmation, 1)
    }

    func testPromptDoesNotDuplicateTheLatestMessage() async {
        let runtime = FakeAICoreRuntime(state: .available, response: "On-device answer")
        let viewModel = makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        viewModel.messages = [.init(role: "assistant", content: "Earlier reply")]
        viewModel.draft = "Latest question"

        await viewModel.sendOnDevice()

        let prompt = await runtime.generationSnapshot().lastPrompt
        XCTAssertEqual(prompt?.components(separatedBy: "Latest question").count, 2)
    }

    func testCleartextPolicyReliesOnSharedEndpointValidatorForLANHosts() throws {
        let manifestURL = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appending(path: "Android/app/src/main/AndroidManifest.xml")
        let manifest = try String(contentsOf: manifestURL, encoding: .utf8)
        XCTAssertTrue(manifest.contains("android:usesCleartextTraffic=\"true\""))
        XCTAssertFalse(manifest.contains("android:networkSecurityConfig"))

        let contract = try LocalLLMEndpointPolicyContract.load()
        let rows = contract.hostPolicyRows(for: "swift-android")
        XCTAssertEqual(Set(rows.map(\.id)), [
            "rfc1918-192-private",
            "dot-local-hostname",
            "single-label-hostname",
            "public-hostname-rejected"
        ])

        for row in rows {
            let result = LocalLLMEndpointProfile.responsesURL(from: row.endpoint)
            if row.isAllowed {
                XCTAssertNotNil(result, "\(row.id): \(row.notes)")
            } else {
                XCTAssertNil(result, "\(row.id): \(row.notes)")
            }
        }
    }

    func testAICoreBridgeUsesTheKotlinObjectJVMStaticSurface() throws {
        let packageURL = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let swiftSource = try String(
            contentsOf: packageURL.appending(path: "Sources/VouchaAndroid/AndroidAICore.swift"),
            encoding: .utf8
        )
        let kotlinSource = try String(
            contentsOf: packageURL.appending(path: "Android/app/src/main/kotlin/AICoreBridge.kt"),
            encoding: .utf8
        )

        XCTAssertTrue(kotlinSource.contains("object AICoreBridge"))
        XCTAssertTrue(kotlinSource.contains("@JvmStatic"))
        XCTAssertTrue(swiftSource.contains("D.voucha.android.AICoreBridge.INSTANCE()"))
        XCTAssertFalse(swiftSource.contains("AICoreBridge.Companion"))
    }

    private func makeViewModel(runtime: FakeAICoreRuntime) -> ViewModel {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        return ViewModel(
            runtime: runtime,
            secretStore: TestEndpointSecrets(),
            settingsStore: LocalLLMSettingsStore(
                fileURL: fileURL,
                secretStore: InMemoryLocalLLMSecretStore()
            ),
            endpointClient: TestEndpointClient()
        )
    }
}

enum TestError: Error {
    case failed
}

private final class TestEndpointSecrets: AndroidEndpointSecretStoring {
    func string(forKey _: String) throws -> String? {
        nil
    }

    func set(_: String, forKey _: String) throws {}

    func removeValue(forKey _: String) throws {}
}

private actor TestEndpointClient: AndroidEndpointGenerating {
    func generateAssistantResponse(
        message _: String,
        history _: [LocalLLMChatMessage],
        endpoint _: LocalLLMEndpointProfile,
        apiKey _: String?
    ) async throws -> String? {
        throw TestError.failed
    }
}

actor FakeAICoreRuntime: AndroidAICoreRunning {
    var state: AndroidModelState
    let response: String
    let error: Error?
    var downloadCallCount = 0
    var generateCallCount = 0
    var lastPrompt: String?

    init(state: AndroidModelState, response: String = "", error: Error? = nil) {
        self.state = state
        self.response = response
        self.error = error
    }

    func status() async throws -> AndroidModelState {
        state
    }

    func download() async throws -> AndroidModelState {
        downloadCallCount += 1
        state = .available
        return state
    }

    func generate(prompt: String) async throws -> String {
        generateCallCount += 1
        lastPrompt = prompt
        if let error {
            throw error
        }
        return response
    }

    func generationSnapshot() -> (callCount: Int, lastPrompt: String?) {
        (generateCallCount, lastPrompt)
    }

    func downloadCount() -> Int {
        downloadCallCount
    }
}
