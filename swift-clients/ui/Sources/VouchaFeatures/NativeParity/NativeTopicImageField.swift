import Observation
import SwiftUI
import UniformTypeIdentifiers
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class NativeTopicImageFieldUploadState {
    var isUploading = false
    var uploadError: UiVerbatimText?
    var localPreviewData: Data?
    var previewImageId: String?
    var generation = 0
}

struct NativeTopicImageField: View {
    @Environment(\.locale)
    var nativeUiLocale
    let title: UiMessageKey
    let previewWidth: CGFloat
    @Binding
    var imageId: String
    @Binding
    var placement: ImagePlacement?
    private let imageBaseURL: URL
    private let imageUploadService: NativeTopicImageUploadService
    @State
    private var showingImporter = false
    @State
    private var uploadState: NativeTopicImageFieldUploadState

    init(
        title: UiMessageKey,
        previewWidth: CGFloat,
        imageId: Binding<String>,
        placement: Binding<ImagePlacement?>,
        client: APIClient?,
        uploadSession: URLSession = .shared,
        imageBaseURL: URL = AppConfig.shared.imageBaseURL,
        uploadState: NativeTopicImageFieldUploadState? = nil
    ) {
        self.title = title
        self.previewWidth = previewWidth
        _imageId = imageId
        _placement = placement
        _uploadState = State(initialValue: uploadState ?? NativeTopicImageFieldUploadState())
        self.imageBaseURL = imageBaseURL
        imageUploadService = NativeTopicImageUploadService(client: client, session: uploadSession)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(title, locale: nativeUiLocale))
                .font(Typography.subheadline)
            HStack(alignment: .center, spacing: Spacing.sm) {
                if let localPreviewData = uploadState.localPreviewData {
                    LocalImagePreview(data: localPreviewData, uploadComplete: uploadState.previewImageId != nil)
                        .frame(width: previewWidth, height: 48)
                        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                } else if placement?.imageId == imageId.trimmed,
                          let url = imageURL(forPlacement: placement) {
                    AsyncImageView(urlString: url, baseURL: imageBaseURL, contentMode: .fill)
                        .frame(width: previewWidth, height: 48)
                        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    TextField(UiMessages.string(
                        .nativeSwiftCommonFieldId,
                        parameters: ["title": UiMessages.string(title, locale: nativeUiLocale)],
                        locale: nativeUiLocale
                    ), text: $imageId)
                        .textFieldStyle(.roundedBorder)
                    HStack(spacing: Spacing.sm) {
                        Button(UiMessages.string(
                            imageId.trimmed.isEmpty
                                ? .nativeSwiftTopicManagementFieldsUpload
                                : .nativeSwiftTopicManagementFieldsReplace,
                            locale: nativeUiLocale
                        )) {
                            showingImporter = true
                        }
                        .buttonStyle(.bordered)
                        .disabled(uploadState.isUploading)
                        if !imageId.trimmed.isEmpty {
                            Button(UiMessages.string(.nativeSwiftCommonRemove, locale: nativeUiLocale)) {
                                imageId = ""
                                placement = nil
                                uploadState.localPreviewData = nil
                                uploadState.previewImageId = nil
                            }
                            .buttonStyle(.bordered)
                            .disabled(uploadState.isUploading)
                        }
                    }
                }
                Spacer(minLength: 0)
            }
            if uploadState.isUploading {
                ProgressView()
            }
            if let uploadError = uploadState.uploadError {
                Text(verbatim: UiMessages.string(uploadError, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
        .fileImporter(isPresented: $showingImporter, allowedContentTypes: [.image]) { result in
            switch result {
            case let .success(url):
                Task { await uploadImage(at: url) }
            case let .failure(error):
                uploadState.uploadError = .verbatim(error.localizedDescription)
            }
        }
        .onDisappear {
            uploadState.generation += 1
            uploadState.isUploading = false
            uploadState.localPreviewData = nil
            uploadState.previewImageId = nil
        }
        .onChange(of: imageId) { _, newValue in handleImageIdChange(newValue) }
        .onChange(of: placement) { _, newValue in
            if newValue?.imageId == imageId.trimmed {
                uploadState.localPreviewData = nil
                uploadState.previewImageId = nil
            }
        }
    }

    private func imageURL(forPlacement placement: ImagePlacement?) -> String? {
        AppConfig(baseURL: AppConfig.shared.baseURL, imageBaseURL: imageBaseURL)
            .imageURL(for: placement, width: 96)
    }

}

extension NativeTopicImageField {
    func handleImageIdChange(_ newValue: String) {
        if newValue != uploadState.previewImageId {
            if uploadState.isUploading, uploadState.previewImageId == nil {
                uploadState.generation += 1
                uploadState.isUploading = false
            }
            uploadState.localPreviewData = nil
            uploadState.previewImageId = nil
        }
        if placement?.imageId != newValue.trimmed {
            placement = nil
        }
    }

    func uploadImage(at url: URL) async {
        uploadState.isUploading = true
        uploadState.uploadError = nil
        uploadState.generation += 1
        let generation = uploadState.generation
        defer {
            if generation == uploadState.generation {
                uploadState.isUploading = false
            }
        }
        do {
            uploadState.localPreviewData = nil
            uploadState.previewImageId = nil
            let (data, contentType) = try await ImageSelectionLoader.load(from: url)
            guard generation == uploadState.generation else { return }
            uploadState.localPreviewData = data
            let uploadedImageId = try await imageUploadService.uploadImage(data: data, contentType: contentType)
            guard generation == uploadState.generation else { return }
            uploadState.previewImageId = uploadedImageId
            imageId = uploadedImageId
        } catch {
            guard generation == uploadState.generation else { return }
            uploadState.localPreviewData = nil
            uploadState.previewImageId = nil
            uploadState.uploadError = .verbatim(error.localizedDescription)
        }
    }
}
