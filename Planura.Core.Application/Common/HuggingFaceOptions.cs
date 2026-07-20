namespace Planura.Core.Application.Common;

public class HuggingFaceOptions
{
    public const string SectionName = "HuggingFace";

    public string ApiKey { get; set; } = string.Empty;

    // Ideally black-forest-labs/FLUX.1-Kontext-dev (image + text prompt in,
    // edited image out) via a third-party provider (fal-ai/replicate/
    // wavespeed), which is the true image-to-image path for this feature.
    // Third-party "Routed by Hugging Face" providers require account-level
    // access beyond the gated-model license (confirmed via direct API
    // testing: every third-party provider rejects every model, gated or
    // not, with "Model not supported by provider <x>", while hf-inference
    // works fine on the same token) that we couldn't unblock, so this
    // defaults to a text-to-image model that is actually live on
    // hf-inference instead. Trade-off: hf-inference has zero image-to-image
    // models at all (checked via
    // https://huggingface.co/api/models?inference_provider=hf-inference&pipeline_tag=image-to-image
    // returning []), so the uploaded venue photo can no longer guide the
    // output - the app treats it as a local "before" reference only. Swap
    // Model/BaseUrl back to FLUX.1-Kontext-dev + a third-party provider
    // once that account-level access is sorted out.
    public string Model { get; set; } = "stabilityai/stable-diffusion-3-medium-diffusers";

    // Serverless Inference API host, routed through HF's unified Inference
    // Providers router. The older api-inference.huggingface.co host has
    // been retired (no longer resolves). The path segment right after
    // router.huggingface.co is a specific *provider* name - "hf-inference"
    // is HF's own first-party provider and the only one confirmed working
    // for this account/token; third-party provider names (fal-ai,
    // replicate, wavespeed, ...) go in this same slot once account-level
    // access to them is resolved.
    public string BaseUrl { get; set; } = "https://router.huggingface.co/hf-inference/models";
}
