import Foundation
import Observation
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class SourceCreationViewModel {
    var url = ""
    private(set) var importId: String?
    private(set) var submitted = false
    private(set) var result: ImportResult?
    private(set) var isWorking = false
    private(set) var message: UiVerbatimText?
    private(set) var isTerminal = false
    private(set) var destination: String?
    let service: SourceCreationService
    let baseURL: URL
    let identity: @MainActor () -> String?
    let delay: @Sendable () async throws -> Void
    let pollLimit: Int
    private var generation = 0
    private var owner: String?

    init(
        service: SourceCreationService,
        baseURL: URL,
        identity: @escaping @MainActor () -> String?,
        pollLimit: Int = 30,
        delay: @escaping @Sendable () async throws -> Void = {
            try await Task.sleep(nanoseconds: 2_000_000_000)
        }
    ) {
        self.service = service
        self.baseURL = baseURL
        self.identity = identity
        self.pollLimit = pollLimit
        self.delay = delay
    }

    func cancel() {
        generation += 1
        isWorking = false
    }

    func reset() {
        cancel()
        url = ""
        importId = nil
        submitted = false
        result = nil
        message = nil
        isTerminal = false
        destination = nil
        owner = nil
    }

    func create() async {
        guard !isWorking, !submitted, !isTerminal, importId == nil,
              !url.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let account = identity() else { return }
        generation += 1
        let token = generation
        owner = account
        submitted = true
        isWorking = true
        defer { if current(token, account) { isWorking = false } }
        do {
            let accepted = try await service.create(url)
            guard current(token, account) else { return }
            // Retain acceptance even if its status URL is invalid: retry must never submit again.
            importId = accepted.import.id
            guard SourceCreationService.validStatusURL(
                accepted.statusUrl,
                importId: accepted.import.id,
                baseURL: baseURL
            )
            else {
                isTerminal = true
                message = .message(.nativeSwiftImportExportUnknownError)
                return
            }
            await monitor(token: token, account: account)
        } catch {
            guard current(token, account) else { return }
            // A failed POST can have reached the server. Keep this operation single-submit.
            isTerminal = true
            message = .message(.nativeSwiftImportExportUnknownError)
        }
    }

    func resume() async {
        guard !isWorking, importId != nil, !isTerminal, let account = identity(), account == owner else { return }
        generation += 1
        let token = generation
        isWorking = true
        defer { if current(token, account) { isWorking = false } }
        await monitor(token: token, account: account)
    }

    private func current(_ token: Int, _ account: String) -> Bool {
        token == generation && identity() == account && !Task.isCancelled
    }

    private func monitor(token: Int, account: String) async {
        guard let importId else { return }
        message = .message(.nativeSwiftImportExportPending)
        do {
            for attempt in 0 ..< pollLimit {
                let status = try await service.status(importId)
                guard current(token, account) else { return }
                guard status.import.id == importId, status.import.totalRows == 1,
                      !status.pageInfo.hasNextPage, status.rows.count <= 1 else {
                    message = .message(.nativeSwiftImportExportUnknownError)
                    return
                }
                result = status.rows.first
                if status.import.pendingRows == 0 || status.import.completedAt != nil,
                   let result, result.status != .pending {
                    if result.status != .error,
                       let entityId = result.entityId, status.import.failedRows == 0 {
                        let topic = try await service.topic(entityId)
                        guard current(token, account) else { return }
                        destination = topic.map { "/source/\($0)" }
                        isTerminal = true
                        message = .message(.nativeSwiftImportExportSourceCreated)
                    } else {
                        isTerminal = true
                        message = .message(
                            .nativeSwiftImportExportFailed,
                            textParameters: ["error": .message(.nativeSwiftImportExportUnknownError)]
                        )
                    }
                    return
                }
                message = result?.isRetrying == true
                    ? .message(
                        .nativeSwiftImportExportRetrying,
                        textParameters: ["error": .message(.nativeSwiftImportExportUnknownError)]
                    )
                    : .message(.nativeSwiftImportExportPending)
                if attempt + 1 < pollLimit { try await delay() }
            }
            message = .message(.nativeSwiftImportExportTryStatusAgain)
        } catch {
            guard current(token, account) else { return }
            message = .message(.nativeSwiftImportExportTryStatusAgain)
        }
    }
}
