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
        images = []
        isUploadingImages = false
        imageUploadErrorMessage = nil
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

    func uploadImages(from urls: [URL]) async {
        guard !isLoading else {
            imageUploadErrorMessage = .app(UiMessage(.nativeSwiftPostComposeImageWaitForPublishing))
            return
        }

        guard let imageUploadService else {
            imageUploadErrorMessage = .app(UiMessage(.nativeSwiftImageSelectionRequiresSignedInSession))
            return
        }

        imageUploadErrorMessage = nil
        beginImageUploadBatch()
        defer { endImageUploadBatch() }

        for url in urls {
            guard !Task.isCancelled else { break }
            guard canAddMoreImages else {
                reportImageUploadError(.app(UiMessage(.nativeSwiftPostComposeImageMaximumReached)))
                break
            }
            do {
                let (data, contentType) = try await ImageSelectionLoader.load(from: url)
                let state = try await imageUploadService.uploadImage(data: data, contentType: contentType)
                guard state.ready || (state.uploadStatus == .complete && !state.blocked) else {
                    reportImageUploadError(state.blocked
                        ? .app(UiMessage(.nativeSwiftImageSelectionBlocked))
                        : state.uploadError.map { UiVerbatimText.verbatim($0) }
                        ?? .app(UiMessage(.nativeSwiftImageSelectionUploadFailed)))
                    continue
                }
                guard !images.contains(where: { $0.imageId == state.id }) else {
                    continue
                }
                guard canAddMoreImages else {
                    reportImageUploadError(.app(UiMessage(.nativeSwiftPostComposeImageMaximumReached)))
                    break
                }
                images.append(.init(imageId: state.id))
            } catch let error as ImageSelectionError {
                reportImageUploadError(.app(error.message))
            } catch {
                reportImageUploadError(.verbatim(error.localizedDescription))
            }
        }
    }

    private func reportImageUploadError(_ message: UiVerbatimText) {
        if imageUploadErrorMessage == nil {
            imageUploadErrorMessage = message
        }
    }

}
