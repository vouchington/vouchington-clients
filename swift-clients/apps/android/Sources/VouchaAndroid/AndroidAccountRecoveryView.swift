import SwiftUI
import VouchaLocalization

struct AndroidAccountRecoveryView: View {
    @Bindable var account: AndroidAccount
    let isReplacing: Bool
    let discardUnreadableSnapshot: () -> Void
    @Environment(\.locale) var locale

    var body: some View {
        Group {
            if let error = account.cookiePersistenceNotice.errorMessage {
                Text(verbatim: UiMessages.string(.verbatim(error), locale: locale))
                    .foregroundStyle(.red)
                Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                    Task { await account.retryCookiePersistenceOrRestoration() }
                }
                .disabled(isReplacing || account.isRestoringSession)
                if account.cookiePersistenceNotice.canDiscardUnreadableSnapshot {
                    Button(UiMessages.string(.nativeSwiftProfileSignOut, locale: locale), role: .destructive) {
                        discardUnreadableSnapshot()
                    }
                    .disabled(isReplacing || account.isRestoringSession)
                }
            }
            if let error = account.sessionRestoreError {
                Text(verbatim: UiMessages.string(error, locale: locale)).foregroundStyle(.red)
                Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                    Task { await account.restore() }
                }
                .disabled(isReplacing || account.isRestoringSession)
            }
        }
    }
}
