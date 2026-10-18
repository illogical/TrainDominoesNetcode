// macOS built-in Vision OCR. Reads actual captured pixels, not scene text metadata.
import Foundation
import Vision
import ImageIO

for path in CommandLine.arguments.dropFirst() {
    let request = VNRecognizeTextRequest()
    request.recognitionLevel = .accurate
    request.usesLanguageCorrection = false
    do {
        try VNImageRequestHandler(url: URL(fileURLWithPath: path)).perform([request])
        let text = (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator: "\n")
        let data = try JSONSerialization.data(withJSONObject: ["file": path, "text": text], options: [.sortedKeys])
        print(String(data: data, encoding: .utf8)!)
    } catch {
        fputs("OCR failed: \(path): \(error)\n", stderr)
        exit(1)
    }
}
