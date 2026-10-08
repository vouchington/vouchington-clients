import SwiftUI
import VouchaLocalization

#if canImport(AppKit)
    import AppKit
#elseif canImport(UIKit)
    import UIKit
#endif

struct LocalImagePreview: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let data: Data
    var contentMode: ContentMode = .fill
    var uploadComplete = true

    var body: some View {
        Group {
            if let image = Self.decode(data) {
                image
                    .resizable()
                    .aspectRatio(contentMode: contentMode)
                    .accessibilityHidden(true)
            } else {
                VStack(spacing: 4) {
                    Image(systemName: "photo")
                        .accessibilityHidden(true)
                    Text(UiMessages.string(
                        uploadComplete
                            ? UiMessageKey.imagesUploadPreviewUnavailable
                            : UiMessageKey.nativeSwiftMarkdownEditorPreviewUnavailable,
                        locale: nativeUiLocale
                    ))
                    .font(.caption)
                    .multilineTextAlignment(.center)
                }
                .accessibilityElement(children: .combine)
            }
        }
    }

    static func canDecode(_ data: Data) -> Bool {
        decode(data) != nil
    }

    private static func decode(_ data: Data) -> Image? {
        #if canImport(AppKit)
            guard let image = NSImage(data: data) else { return nil }
            return Image(nsImage: image)
        #elseif canImport(UIKit)
            guard let image = UIImage(data: data) else { return nil }
            return Image(uiImage: image)
        #elseif SKIP
            guard let image = UIImage(data: data) else { return nil }
            return Image(uiImage: image)
        #else
            nil
        #endif
    }
}
