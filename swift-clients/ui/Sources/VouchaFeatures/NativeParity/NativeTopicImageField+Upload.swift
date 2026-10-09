import Foundation

extension NativeTopicImageField {
    func beginUpload(at url: URL) {
        guard !isEditingDisabled(), !uploadState.isUploading, uploadState.uploadTask == nil else { return }
        uploadState.uploadTask = Task { await uploadImage(at: url) }
    }

    func cancelUpload() {
        uploadState.generation += 1
        uploadState.uploadTask?.cancel()
        uploadState.localPreviewData = nil
        uploadState.previewImageId = nil
        // Keep the field busy until the canceled operation releases its bytes and request.
    }

    func handleImageIdChange(_ newValue: String) {
        if newValue != uploadState.previewImageId {
            if uploadState.isUploading, uploadState.previewImageId == nil {
                cancelUpload()
            }
            uploadState.localPreviewData = nil
            uploadState.previewImageId = nil
        }
        if placement?.imageId != newValue.trimmed {
            placement = nil
        }
    }

    func uploadImage(at url: URL) async {
        guard !Task.isCancelled else {
            uploadState.uploadTask = nil
            return
        }
        guard !uploadState.isUploading else { return }
        uploadState.isUploading = true
        uploadState.uploadError = nil
        uploadState.generation += 1
        let generation = uploadState.generation
        defer {
            uploadState.isUploading = false
            uploadState.uploadTask = nil
        }
        do {
            uploadState.localPreviewData = nil
            uploadState.previewImageId = nil
            try Task.checkCancellation()
            let (data, contentType) = try await ImageSelectionLoader.load(from: url)
            try Task.checkCancellation()
            guard generation == uploadState.generation else { return }
            uploadState.localPreviewData = data
            let uploadedImageId = try await imageUploadService.uploadImage(data: data, contentType: contentType)
            guard generation == uploadState.generation else { return }
            uploadState.localPreviewData = LocalImagePreview.thumbnailData(from: data)
            uploadState.previewImageId = uploadedImageId
            imageId = uploadedImageId
        } catch {
            guard generation == uploadState.generation, !Task.isCancelled else { return }
            uploadState.localPreviewData = nil
            uploadState.previewImageId = nil
            uploadState.uploadError = .verbatim(error.localizedDescription)
        }
    }
}
