import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativePostComposeViewModel {
    var hasImages: Bool {
        !images.isEmpty
    }

    var canAddMoreImages: Bool {
        images.count < 20
    }

    var imageInputs: [CreatePostImageInput] {
        images.enumerated().map { index, image in
            .init(imageId: image.imageId, orderIndex: index, caption: image.publishCaption)
        }
    }

    var imageSummary: UiVerbatimText? {
        guard hasImages else { return nil }
        return .count(images.count, item: "image")
    }

    func resetImageFields() {
        imageUploadGeneration += 1
        images = []
        pendingImagePreviews = []
        activeImageUploadBatches = 0
        isUploadingImages = false
        imageUploadErrorMessage = nil
    }

    func clearImagePreviewsForNavigation() {
        imageUploadGeneration += 1
        imageUploadTask?.cancel()
        for index in images.indices {
            images[index].localPreviewData = nil
        }
        pendingImagePreviews = []
    }

    func removeImage(id: UUID) {
        guard let index = images.firstIndex(where: { $0.id == id }) else { return }
        images.remove(at: index)
    }

    func moveImageUp(id: UUID) {
        guard let index = images.firstIndex(where: { $0.id == id }), index > 0 else { return }
        images.swapAt(index - 1, index)
    }

    func moveImageDown(id: UUID) {
        guard let index = images.firstIndex(where: { $0.id == id }), index < images.count - 1 else { return }
        images.swapAt(index, index + 1)
    }

    func updateImageCaption(id: UUID, caption: String) {
        guard let index = images.firstIndex(where: { $0.id == id }) else { return }
        images[index].updateCaption(caption)
    }

    func startImageUploadBatch(from urls: [URL]) {
        guard imageUploadTask == nil, !isUploadingImages else { return }
        guard !isLoading else {
            imageUploadErrorMessage = .app(UiMessage(.nativeSwiftPostComposeImageWaitForPublishing))
            return
        }
        guard let imageUploadService else {
            imageUploadErrorMessage = .app(UiMessage(.nativeSwiftImageSelectionRequiresSignedInSession))
            return
        }

        imageUploadErrorMessage = nil
        imageUploadGeneration += 1
        let generation = imageUploadGeneration
        beginImageUploadBatch()
        imageUploadTask = Task {
            defer {
                endImageUploadBatch()
                imageUploadTask = nil
            }
            await performImageUploads(from: urls, generation: generation, service: imageUploadService)
        }
    }

    func uploadImages(from urls: [URL]) async {
        guard !isUploadingImages, imageUploadTask == nil else { return }
        guard !isLoading else {
            imageUploadErrorMessage = .app(UiMessage(.nativeSwiftPostComposeImageWaitForPublishing))
            return
        }

        guard let imageUploadService else {
            imageUploadErrorMessage = .app(UiMessage(.nativeSwiftImageSelectionRequiresSignedInSession))
            return
        }

        imageUploadErrorMessage = nil
        imageUploadGeneration += 1
        let generation = imageUploadGeneration
        beginImageUploadBatch()
        defer { endImageUploadBatch() }

        await performImageUploads(from: urls, generation: generation, service: imageUploadService)
    }

    private func performImageUploads(from urls: [URL], generation: Int, service: ImageUploadService) async {
        for url in urls {
            guard !Task.isCancelled, generation == imageUploadGeneration else { break }
            guard canAddMoreImages else {
                reportImageUploadError(.app(UiMessage(.nativeSwiftPostComposeImageMaximumReached)))
                break
            }
            guard await uploadImage(from: url, generation: generation, service: service) else { break }
        }
    }

    private func uploadImage(from url: URL, generation: Int, service: ImageUploadService) async -> Bool {
        do {
            let (data, contentType) = try await ImageSelectionLoader.load(from: url)
            guard generation == imageUploadGeneration else { return false }
            let thumbnail = LocalImagePreview.thumbnailData(from: data)
            let pendingPreviewId = UUID()
            pendingImagePreviews.append(.init(id: pendingPreviewId, data: thumbnail))
            let state = try await service.uploadImage(data: data, contentType: contentType)
            guard generation == imageUploadGeneration else { return false }
            pendingImagePreviews.removeAll { $0.id == pendingPreviewId }
            guard state.ready || (state.uploadStatus == .complete && !state.blocked) else {
                reportImageUploadError(state.blocked
                    ? .app(UiMessage(.nativeSwiftImageSelectionBlocked))
                    : state.uploadError.map { UiVerbatimText.verbatim($0) }
                    ?? .app(UiMessage(.nativeSwiftImageSelectionUploadFailed)))
                return true
            }
            guard !images.contains(where: { $0.imageId == state.id }) else { return true }
            guard canAddMoreImages else {
                reportImageUploadError(.app(UiMessage(.nativeSwiftPostComposeImageMaximumReached)))
                return false
            }
            images.append(.init(imageId: state.id, localPreviewData: thumbnail))
        } catch let error as ImageSelectionError {
            guard generation == imageUploadGeneration else { return false }
            pendingImagePreviews.removeAll()
            reportImageUploadError(.app(error.message))
        } catch {
            guard generation == imageUploadGeneration else { return false }
            pendingImagePreviews.removeAll()
            reportImageUploadError(.verbatim(error.localizedDescription))
        }
        return true
    }

    private func reportImageUploadError(_ message: UiVerbatimText) {
        if imageUploadErrorMessage == nil {
            imageUploadErrorMessage = message
        }
    }
}

extension String {
    var trimmedOrNil: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
