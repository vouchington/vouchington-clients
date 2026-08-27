import SwiftUI
import UniformTypeIdentifiers
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct NativeTopicImageField: View {
    @Environment(\.locale)
    var nativeUiLocale
    let title: UiMessageKey
    let previewWidth: CGFloat
    @Binding
    var imageId: String
    private let imageBaseURL: URL
    private let imageUploadService: NativeTopicImageUploadService
    @State
    private var showingImporter = false
    @State
    private var isUploading = false
    @State
    private var uploadError: UiVerbatimText?

    init(
        title: UiMessageKey,
        previewWidth: CGFloat,
        imageId: Binding<String>,
        client: APIClient?,
        uploadSession: URLSession = .shared,
        imageBaseURL: URL = AppConfig.shared.imageBaseURL
    ) {
        self.title = title
        self.previewWidth = previewWidth
        _imageId = imageId
        self.imageBaseURL = imageBaseURL
        imageUploadService = NativeTopicImageUploadService(client: client, session: uploadSession)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(title, locale: nativeUiLocale))
                .font(Typography.subheadline)
            HStack(alignment: .center, spacing: Spacing.sm) {
                if !imageId.trimmed.isEmpty, let url = imageURL(forImageId: imageId) {
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
                        .disabled(isUploading)
                        if !imageId.trimmed.isEmpty {
                            Button(UiMessages.string(.nativeSwiftCommonRemove, locale: nativeUiLocale)) {
                                imageId = ""
                            }
                            .buttonStyle(.bordered)
                            .disabled(isUploading)
                        }
                    }
                }
                Spacer(minLength: 0)
            }
            if isUploading {
                ProgressView()
            }
            if let uploadError {
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
                uploadError = .verbatim(error.localizedDescription)
            }
        }
    }

    private func imageURL(forImageId imageId: String) -> String? {
        var components = URLComponents(url: imageBaseURL, resolvingAgainstBaseURL: false)
        components?.path += "/images/\(imageId)"
        components?.queryItems = [URLQueryItem(name: "w", value: "96")]
        return components?.url?.absoluteString
    }

    private func uploadImage(at url: URL) async {
        isUploading = true
        uploadError = nil
        defer { isUploading = false }
        do {
            imageId = try await imageUploadService.uploadImage(at: url)
        } catch {
            uploadError = .verbatim(error.localizedDescription)
        }
    }
}
