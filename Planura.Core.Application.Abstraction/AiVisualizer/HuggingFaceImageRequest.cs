namespace Planura.Core.Application.Abstraction.AiVisualizer
{
    // Wire-level request for the provider call: the raw bytes of the
    // uploaded venue photo plus the client's freeform styling instructions.
    public class HuggingFaceImageRequest
    {
        public byte[] ImageBytes { get; set; } = System.Array.Empty<byte>();
        public string ImageContentType { get; set; } = "image/jpeg";
        public string Prompt { get; set; } = string.Empty;
    }
}
