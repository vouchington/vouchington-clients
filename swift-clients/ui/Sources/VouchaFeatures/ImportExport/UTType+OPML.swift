import UniformTypeIdentifiers

extension UTType {
    static let vouchaOPML: UTType = {
        guard let type = UTType(filenameExtension: "opml", conformingTo: .xml) else {
            preconditionFailure("Uniform Type Identifiers must support the OPML filename extension")
        }
        return type
    }()
}
