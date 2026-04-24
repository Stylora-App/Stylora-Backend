namespace Stylora.Application.Interfaces;

public interface IImageEmbeddingService
{
    Task<float[]> EmbedImageAsync(string imageBase64, CancellationToken cancellationToken = default);
}
