import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels

enum SpendingCategoryTestError: Error {
    case expected
}

@MainActor
final class SpendingServiceStub: SpendingCategoryServicing {
    var pages: [Result<SpendingCategoryPage, Error>]
    var created: SpendingCategory?
    var updated: SpendingCategory?
    var searchResult: Result<[TopicSearchResult], Error> = .success([])
    var deleteResult: Result<Void, Error> = .success(())
    var createDelay = false
    var deleteDelay = false
    var searchDelay = false
    var createCalls = 0
    var updateCalls = 0
    var searchCalls = 0
    var deleteCalls = 0
    private var createContinuation: CheckedContinuation<Void, Never>?
    private var deleteContinuation: CheckedContinuation<Void, Never>?
    private var searchContinuation: CheckedContinuation<Void, Never>?

    init(
        pages: [Result<SpendingCategoryPage, Error>] = [],
        created: SpendingCategory? = nil,
        updated: SpendingCategory? = nil
    ) {
        self.pages = pages
        self.created = created
        self.updated = updated
    }

    func categories(after _: String?) async throws -> SpendingCategoryPage {
        guard !pages.isEmpty else { return .init(results: []) }
        return try pages.removeFirst().get()
    }

    func searchTopics(query _: String) async throws -> [TopicSearchResult] {
        searchCalls += 1
        if searchDelay {
            await withCheckedContinuation { searchContinuation = $0 }
        }
        return try searchResult.get()
    }

    func create(body _: CreateSpendingCategoryBody) async throws -> SpendingCategory {
        createCalls += 1
        if createDelay {
            await withCheckedContinuation { createContinuation = $0 }
        }
        guard let created else { throw SpendingCategoryTestError.expected }
        return created
    }

    func update(id _: String, body _: UpdateSpendingCategoryBody) async throws -> SpendingCategory {
        updateCalls += 1
        guard let updated else { throw SpendingCategoryTestError.expected }
        return updated
    }

    func delete(id _: String) async throws {
        deleteCalls += 1
        if deleteDelay {
            await withCheckedContinuation { deleteContinuation = $0 }
        }
        try deleteResult.get()
    }

    func resumeCreate() {
        createDelay = false
        createContinuation?.resume()
        createContinuation = nil
    }

    func resumeDelete() {
        deleteDelay = false
        deleteContinuation?.resume()
        deleteContinuation = nil
    }

    func resumeSearch() {
        searchDelay = false
        searchContinuation?.resume()
        searchContinuation = nil
    }
}
