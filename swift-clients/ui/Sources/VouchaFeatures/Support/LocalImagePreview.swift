import SwiftUI
import VouchaLocalization

#if !SKIP && canImport(ImageIO)
    import ImageIO
    import UniformTypeIdentifiers
#endif

#if canImport(AppKit)
    import AppKit
#elseif canImport(UIKit)
    import UIKit
#endif

struct LocalImagePreview: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let data: Data?
    var contentMode: ContentMode = .fill
    var uploadComplete = true

    var body: some View {
        Group {
            if let data, let image = Self.decode(data) {
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

    static func thumbnailData(from data: Data) -> Data? {
        let maxPixelSize = 128
        let maxBytes = 256 * 1_024

        #if SKIP
            guard let image = UIImage(data: data) else { return nil }
            let largestDimension = max(image.size.width, image.size.height)
            guard largestDimension.isFinite, largestDimension > 0 else { return nil }
            let scale = min(1, CGFloat(maxPixelSize) / largestDimension)
            let size = CGSize(
                width: max(1, (image.size.width * scale).rounded(.down)),
                height: max(1, (image.size.height * scale).rounded(.down))
            )
            guard let thumbnail = image.preparingThumbnail(of: size),
                  let bytes = thumbnail.pngData(),
                  bytes.count <= maxBytes else { return nil }
            return bytes
        #elseif canImport(ImageIO)
            let sourceOptions: [CFString: Any] = [kCGImageSourceShouldCache: false]
            guard let source = CGImageSourceCreateWithData(data as CFData, sourceOptions as CFDictionary)
            else { return nil }
            let thumbnailOptions: [CFString: Any] = [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceShouldCacheImmediately: false,
                kCGImageSourceThumbnailMaxPixelSize: maxPixelSize
            ]
            guard let image = CGImageSourceCreateThumbnailAtIndex(source, 0, thumbnailOptions as CFDictionary),
                  let output = CFDataCreateMutable(kCFAllocatorDefault, 0),
                  let destination = CGImageDestinationCreateWithData(
                      output,
                      UTType.png.identifier as CFString,
                      1,
                      nil
                  ) else { return nil }
            CGImageDestinationAddImage(destination, image, nil)
            guard CGImageDestinationFinalize(destination) else { return nil }
            let length = CFDataGetLength(output)
            guard length > 0, length <= maxBytes,
                  let bytes = CFDataGetBytePtr(output) else { return nil }
            return Data(bytes: bytes, count: length)
        #else
            return nil
        #endif
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
