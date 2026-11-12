import SwiftUI
import VouchaLocalization

struct SourceCreationView: View {
    @Environment(\.locale) var locale
    @Bindable var model: SourceCreationViewModel
    var navigate: (String) -> Void

    var body: some View {
        Form {
            TextField(UiMessages.string(.nativeSwiftHouseholdsBookmarksUrl, locale: locale), text: $model.url)
                .disabled(model.submitted || model.isWorking || model.isTerminal)
            Button(UiMessages.string(.nativeSwiftCommonCreate, locale: locale)) {
                Task { await model.create() }
            }
            .disabled(model.isWorking || model.submitted || model.isTerminal || model.url.isEmpty)
            if model.isWorking {
                ProgressView(UiMessages.string(.nativeSwiftImportExportImportProgress, locale: locale))
                Button(UiMessages.string(.nativeSwiftImportExportStopMonitoring, locale: locale)) { model.cancel() }
            }
            if let message = model.message {
                Text(verbatim: UiMessages.string(message, locale: locale))
            }
            if model.importId != nil, !model.isTerminal, !model.isWorking {
                Button(UiMessages.string(.nativeSwiftImportExportTryStatusAgain, locale: locale)) {
                    Task { await model.resume() }
                }
            }
            if model.currentDestination != nil {
                Button(UiMessages.string(.nativeSwiftPodcastPlaybackOpenSource, locale: locale)) {
                    if let destination = model.currentDestination { navigate(destination) }
                }
            }
        }
        .onDisappear { model.cancel() }
    }
}
