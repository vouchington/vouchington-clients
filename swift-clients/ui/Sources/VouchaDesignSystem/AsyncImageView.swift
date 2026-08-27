import SwiftUI
import VouchaCore

public struct AsyncImageView: View {
    public let urlString: String?
    public let resolvedURLString: String?
    public let contentMode: ContentMode

    public init(urlString: String?, baseURL: URL = AppConfig.shared.baseURL, contentMode: ContentMode = .fill) {
        self.urlString = urlString
        resolvedURLString = VouchaURLResolver.absoluteString(for: urlString, relativeTo: baseURL)
        self.contentMode = contentMode
    }

    private var url: URL? {
        guard let resolvedURLString else { return nil }
        return URL(string: resolvedURLString)
    }

    public var body: some View {
        AsyncImage(url: url) { phase in
            switch phase {
            case .empty:
                RoundedRectangle(cornerRadius: Spacing.sm)
                    .fill(Colors.separator)
                    .overlay {
                        ProgressView()
                            .controlSize(.small)
                    }
            case let .success(image):
                image
                    .resizable()
                    .aspectRatio(contentMode: contentMode)
            case .failure:
                RoundedRectangle(cornerRadius: Spacing.sm)
                    .fill(Colors.separator)
                    .overlay {
                        Image(systemName: "photo")
                            .foregroundStyle(Colors.secondaryLabel)
                    }
            @unknown default:
                RoundedRectangle(cornerRadius: Spacing.sm)
                    .fill(Colors.separator)
            }
        }
    }
}
