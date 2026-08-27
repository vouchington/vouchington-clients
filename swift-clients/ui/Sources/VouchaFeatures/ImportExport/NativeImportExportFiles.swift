import Foundation

enum NativeImportExportFiles {
    static func readUTF8(from url: URL) async throws -> (String, SourceImportFileFormat) {
        let task = Task.detached {
            let accessed = url.startAccessingSecurityScopedResource()
            defer {
                if accessed {
                    url.stopAccessingSecurityScopedResource()
                }
            }
            let fileSize = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize
            if let fileSize, fileSize > ImportExportViewModel.maximumRequestBytes {
                throw ImportExportValidationError.requestTooLarge
            }
            let data = try Data(contentsOf: url)
            guard let text = String(data: data, encoding: .utf8) else {
                throw ImportExportValidationError.invalidUTF8
            }
            try Task.checkCancellation()
            let format: SourceImportFileFormat = url.pathExtension.lowercased() == "csv" ? .csv : .opml
            return (text, format)
        }
        return try await withTaskCancellationHandler {
            try await task.value
        } onCancel: {
            task.cancel()
        }
    }

    static func writeExport(_ data: Data, filename: String, replacing oldURL: URL?) async throws -> URL {
        let task = Task.detached {
            if let oldURL {
                try? FileManager.default.removeItem(at: oldURL)
            }
            let directory = FileManager.default.temporaryDirectory
                .appendingPathComponent("voucha-import-export", isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let url = directory.appendingPathComponent("\(UUID().uuidString)-\(filename)")
            try data.write(to: url, options: .atomic)
            guard !Task.isCancelled else {
                try? FileManager.default.removeItem(at: url)
                throw CancellationError()
            }
            return url
        }
        return try await withTaskCancellationHandler {
            try await task.value
        } onCancel: {
            task.cancel()
        }
    }

    static func remove(_ url: URL?) {
        guard let url else { return }
        try? FileManager.default.removeItem(at: url)
    }
}
