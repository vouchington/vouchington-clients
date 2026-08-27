import Foundation

extension String {
    var trimmed: String {
        trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var nativeLines: [String] {
        split(separator: "\n").map(String.init).map(\.trimmed).filter { !$0.isEmpty }
    }

    func prefixByUTF16Length(_ maxLength: Int) -> String {
        var result = ""
        var length = 0
        for character in self {
            let characterLength = character.utf16.count
            guard length + characterLength <= maxLength else { break }
            result.append(character)
            length += characterLength
        }
        return result
    }
}
