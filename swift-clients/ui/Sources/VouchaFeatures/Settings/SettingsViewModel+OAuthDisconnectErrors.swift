import VouchaLocalization

extension SettingsViewModel {
    func disconnectErrorMessage(for error: Error) -> UiVerbatimText {
        AccountChangeFailureMessage.message(for: error, context: .signInMethodRemoval)
            ?? .verbatim(error.localizedDescription)
    }
}
