using Stylora.Application.Models;

namespace Stylora.Application.Interfaces;

public interface IClothingValidationService
{
    Task<ClothingImageValidationResult> ValidateAsync(string imageBase64, CancellationToken cancellationToken = default);
}
