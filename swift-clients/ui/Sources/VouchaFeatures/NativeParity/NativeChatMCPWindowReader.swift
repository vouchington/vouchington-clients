import SwiftUI

@MainActor
final class NativeChatMCPWindowReference {
    weak var value: AnyObject?
}

struct NativeChatMCPWindowReader: View {
    let onWindowChange: @MainActor (AnyObject?) -> Void

    var body: some View {
        #if os(iOS) && !targetEnvironment(macCatalyst)
            IOSWindowReader(onWindowChange: onWindowChange)
        #elseif os(macOS)
            MacOSWindowReader(onWindowChange: onWindowChange)
        #else
            Color.clear.frame(width: 0, height: 0)
        #endif
    }
}

#if os(iOS) && !targetEnvironment(macCatalyst)
    import UIKit

    private struct IOSWindowReader: UIViewRepresentable {
        let onWindowChange: @MainActor (AnyObject?) -> Void

        func makeUIView(context _: Context) -> WindowView {
            WindowView(onWindowChange: onWindowChange)
        }

        func updateUIView(_ view: WindowView, context _: Context) {
            view.onWindowChange = onWindowChange
        }

        final class WindowView: UIView {
            var onWindowChange: @MainActor (AnyObject?) -> Void

            init(onWindowChange: @escaping @MainActor (AnyObject?) -> Void) {
                self.onWindowChange = onWindowChange
                super.init(frame: .zero)
                isHidden = true
            }

            required init?(coder _: NSCoder) {
                nil
            }

            override func didMoveToWindow() {
                super.didMoveToWindow()
                onWindowChange(window)
            }
        }
    }

#elseif os(macOS)
    import AppKit

    private struct MacOSWindowReader: NSViewRepresentable {
        let onWindowChange: @MainActor (AnyObject?) -> Void

        func makeNSView(context _: Context) -> WindowView {
            WindowView(onWindowChange: onWindowChange)
        }

        func updateNSView(_ view: WindowView, context _: Context) {
            view.onWindowChange = onWindowChange
        }

        final class WindowView: NSView {
            var onWindowChange: @MainActor (AnyObject?) -> Void

            init(onWindowChange: @escaping @MainActor (AnyObject?) -> Void) {
                self.onWindowChange = onWindowChange
                super.init(frame: .zero)
                isHidden = true
            }

            required init?(coder _: NSCoder) {
                nil
            }

            override func viewDidMoveToWindow() {
                super.viewDidMoveToWindow()
                onWindowChange(window)
            }
        }
    }
#endif
