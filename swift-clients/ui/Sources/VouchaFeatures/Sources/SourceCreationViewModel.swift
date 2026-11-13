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
    private var completedForList = false
    var currentDestination: String? {
        identity() == owner && destinationGeneration == generation ? destination : nil
    }

    let service: SourceCreationService
    let baseURL: URL
    let identity: @MainActor () -> String?
    let delay: @Sendable () async throws -> Void
    let pollLimit: Int
    private var generation = 0
    private var owner: String?
    private var destinationGeneration: Int?

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
        if submitted, importId == nil {
            isTerminal = true
            message = .message(.nativeSwiftImportExportUnknownError)
        }
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
        completedForList = false
        destinationGeneration = nil
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

    func consumeListCompletion() -> Bool {
        guard completedForList, identity() == owner else { return false }
        completedForList = false
        return true
    }

    private func current(_ token: Int, _ account: String) -> Bool {
        token == generation && identity() == account && !Task.isCancelled
    }

}

extension SourceCreationViewModel {
    private func complete(_ result: ImportResult, failedRows: Int, token: Int, account: String) async throws {
        if result.status != .error, result.status != .skipped, failedRows == 0 {
            let resolved = try await resolveDestination(result)
            guard current(token, account) else { return }
            guard let resolved else {
                message = .message(.nativeSwiftImportExportTryStatusAgain)
                return
            }
            destination = resolved
            completedForList = true
            destinationGeneration = token
            isTerminal = true
            message = .message(.nativeSwiftImportExportSourceCreated)
        } else {
            isTerminal = true
            message = .message(
                .nativeSwiftImportExportFailed,
                textParameters: ["error": .message(.nativeSwiftImportExportUnknownError)]
            )
        }
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
                    try await complete(result, failedRows: status.import.failedRows, token: token, account: account)
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
