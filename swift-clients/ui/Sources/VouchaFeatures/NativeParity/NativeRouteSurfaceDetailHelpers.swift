import VouchaLocalization

func nativeListItemDetail(_ items: [NativeListItem], item: String) -> UiVerbatimText {
    guard let first = items.first else { return .count(0, item: item) }
    return .joined([
        .count(items.count, item: item),
        .message(.nativeSwiftRouteSurfaceFirstValue, parameters: ["value": first.id])
    ])
}

func asidePreferenceDetail(_ preferences: [NativeAsidePreference]) -> UiVerbatimText {
    guard let first = preferences.first else { return .count(0, item: "item") }
    return .joined([
        .count(preferences.count, item: "item"),
        .message(.nativeSwiftRouteSurfaceFirstValue, parameters: ["value": first.asideKey])
    ])
}
