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
    var uploadTask: Task<Void, Never>?
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
    let isEditingDisabled: () -> Bool
    private let imageBaseURL: URL
    let imageUploadService: NativeTopicImageUploadService
    @State
    private var showingImporter = false
    @State
    var uploadState: NativeTopicImageFieldUploadState

    init(
        title: UiMessageKey,
        previewWidth: CGFloat,
        imageId: Binding<String>,
        placement: Binding<ImagePlacement?>,
        client: APIClient?,
        isEditingDisabled: @escaping () -> Bool = { false },
        uploadSession: URLSession = .shared,
        imageBaseURL: URL = AppConfig.shared.imageBaseURL,
        uploadState: NativeTopicImageFieldUploadState? = nil
    ) {
        self.title = title
        self.previewWidth = previewWidth
        _imageId = imageId
        _placement = placement
        self.isEditingDisabled = isEditingDisabled
        _uploadState = State(initialValue: uploadState ?? NativeTopicImageFieldUploadState())
        self.imageBaseURL = imageBaseURL
        imageUploadService = NativeTopicImageUploadService(client: client, session: uploadSession)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(title, locale: nativeUiLocale))
                .font(Typography.subheadline)
            HStack(alignment: .center, spacing: Spacing.sm) {
                if uploadState.isUploading || uploadState.localPreviewData != nil {
                    LocalImagePreview(
                        data: uploadState.localPreviewData,
                        uploadComplete: uploadState.previewImageId != nil
                    )
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
                        .disabled(isEditingDisabled())
                    HStack(spacing: Spacing.sm) {
                        Button(UiMessages.string(
                            imageId.trimmed.isEmpty
                                ? .nativeSwiftTopicManagementFieldsUpload
                                : .nativeSwiftTopicManagementFieldsReplace,
                            locale: nativeUiLocale
                        )) {
                            guard !isEditingDisabled() else { return }
                            showingImporter = true
                        }
                        .buttonStyle(.bordered)
                        .disabled(uploadState.isUploading || isEditingDisabled())
                        if !imageId.trimmed.isEmpty {
                            Button(UiMessages.string(.nativeSwiftCommonRemove, locale: nativeUiLocale)) {
                                guard !isEditingDisabled() else { return }
                                imageId = ""
                                placement = nil
                                uploadState.localPreviewData = nil
                                uploadState.previewImageId = nil
                            }
                            .buttonStyle(.bordered)
                            .disabled(uploadState.isUploading || isEditingDisabled())
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
                beginUpload(at: url)
            case let .failure(error):
                uploadState.uploadError = .verbatim(error.localizedDescription)
            }
        }
        .onDisappear {
            cancelUpload()
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
