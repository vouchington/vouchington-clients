import Foundation

extension NativePostComposeReviewTopicRatingDraft {
    var hasContent: Bool {
        !topicId.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || rating > 0
    }

    var isValid: Bool {
        !topicId.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && rating > 0
    }
}
