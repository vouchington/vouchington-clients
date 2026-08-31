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
            let file = try FileHandle(forReadingFrom: url)
            defer { try? file.close() }
            var data = Data()
            while let chunk = try file.read(upToCount: 64 * 1_024), !chunk.isEmpty {
                guard data.count + chunk.count <= ImportExportViewModel.maximumRequestBytes else {
                    throw ImportExportValidationError.requestTooLarge
                }
                data.append(chunk)
                try Task.checkCancellation()
            }
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

    static func remove(_ url: URL?) {
        guard let url else { return }
        try? FileManager.default.removeItem(at: url)
    }
}
