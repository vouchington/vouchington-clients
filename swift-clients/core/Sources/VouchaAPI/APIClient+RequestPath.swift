import Foundation

extension APIClient {
    private static let pathSegmentAllowedCharacters: CharacterSet = {
        var characters = CharacterSet.urlPathAllowed
        characters.remove(charactersIn: "/")
        return characters
    }()

    func percentEncodedEndpointPath(_ path: String) -> String {
        path.split(separator: "/", omittingEmptySubsequences: false)
            .map { segment in
                let decodedSegment = String(segment).removingPercentEncoding ?? String(segment)
                return decodedSegment.addingPercentEncoding(withAllowedCharacters: Self.pathSegmentAllowedCharacters)
                    ?? decodedSegment
            }
            .joined(separator: "/")
    }
}
