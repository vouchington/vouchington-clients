import Foundation
import VouchaModels

struct SourceImportPages {
    var cursor: String?
    var cursors: Set<String> = []
    var rows: [ImportResult] = []
    var positions: [String: Int] = [:]
    var summary: RssFeedImportSummary?
    var startedTerminal = false

    mutating func accept(_ page: RssFeedImportStatus) throws -> RssFeedImportStatus? {
        if let summary {
            guard page.import.totalRows == summary.totalRows,
                  page.import.completedRows + page.import.failedRows >= summary.completedRows + summary.failedRows,
                  page.import.pendingRows <= summary.pendingRows
            else { throw URLError(.badServerResponse) }
        }
        let next = page.pageInfo.hasNextPage ? page.pageInfo.endCursor : nil
        if page.pageInfo.hasNextPage {
            guard !page.rows.isEmpty, let next, !next.isEmpty, !cursors.contains(next)
            else { throw URLError(.badServerResponse) }
        }
        if summary == nil { startedTerminal = page.import.pendingRows == 0 || page.import.completedAt != nil }
        for row in page.rows {
            if let id = row.id, let position = positions[id] {
                rows[position] = row
            } else {
                if let id = row.id { positions[id] = rows.count }
                rows.append(row)
            }
        }
        summary = page.import
        cursor = next
        if let next { cursors.insert(next) }
        return next == nil ? RssFeedImportStatus(import: page.import, rows: rows, pageInfo: page.pageInfo) : nil
    }
}

extension ImportExportViewModel {
    /// One bounded server page per tick. Failed pages retain their opaque cursor and prior rows.
    func loadSourcePages(batchId: String, generation: Int) async throws -> RssFeedImportStatus? {
        let page = try await service.sourceStatus(batchId, sourcePages.cursor)
        try Task.checkCancellation()
        guard generation == self.generation, batchId == self.batchId else { throw CancellationError() }
        guard page.import.id == batchId else {
            if sourcePages.cursor == nil { return nil }
            throw URLError(.badServerResponse)
        }
        let snapshot = try sourcePages.accept(page)
        if snapshot != nil {
            sourceSnapshotStartedTerminal = sourcePages.startedTerminal
            sourcePages = SourceImportPages()
        }
        return snapshot
    }
}
