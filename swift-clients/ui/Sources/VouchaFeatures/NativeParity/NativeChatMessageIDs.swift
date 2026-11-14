import Foundation
import VouchaCore

enum NativeChatMessageIDs {
    static func nextLocalTurn() -> (user: String, assistant: String) {
        let (user, assistant) = UuidV7.nextSequentialPair()
        return (user: user, assistant: assistant)
    }
}
