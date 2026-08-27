import VouchaLocalization

extension NativeRouteDestinationIdentifier {
    func row(_ icon: String, _ title: UiMessageKey, _ detail: UiMessageKey) -> NativeRouteDestinationRow {
        .init(icon: icon, title: UiMessage(title), detail: UiMessage(detail))
    }
}
