import SwiftUI
import VouchaDesignSystem
import VouchaModels

struct NativePostAuthorBio: View {
    let aside: PostAuthorAside?

    var body: some View {
        if let aside {
            NativeHtmlContent(html: aside.aboutHtml, detectedLanguage: aside.linguaRsDetectedLanguage)
        }
    }
}
