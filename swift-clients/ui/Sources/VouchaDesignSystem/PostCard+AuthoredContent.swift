import Foundation

extension PostCard {
    var authoredTitle: String? {
        normalizedAuthoredText(post.title)
    }

    var authoredMarkdown: String? {
        normalizedAuthoredText(post.markdown)
    }

    func normalizedAuthoredText(_ value: String?) -> String? {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }
        return value
    }
}
