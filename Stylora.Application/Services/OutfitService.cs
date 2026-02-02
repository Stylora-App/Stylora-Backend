using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Application.Services;

public class OutfitService
{
    private readonly IGeminiService _geminiService;
    private readonly IOutfitRepository _outfitRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWardrobeRepository _wardrobeRepository;

    public OutfitService(
        IGeminiService geminiService, 
        IOutfitRepository outfitRepository,
        IUserRepository userRepository,
        IWardrobeRepository wardrobeRepository)
    {
        _geminiService = geminiService;
        _outfitRepository = outfitRepository;
        _userRepository = userRepository;
        _wardrobeRepository = wardrobeRepository;
    }

    public async Task<OutfitSuggestionResponse> SuggestOutfitAsync(string userId, OutfitSuggestionRequest request)
    {
        var wardrobeItems = request.Items.Select(dto =>
        {
            if (!Enum.TryParse<ClothingCategory>(dto.Category, true, out var category))
            {
                category = ClothingCategory.Top;
            }

            return new WardrobeItem
            {
                Id = Guid.TryParse(dto.Id, out var id) ? id : Guid.NewGuid(),
                Category = category,
                Description = dto.Description
            };
        });

        var suggestion = await _geminiService.SuggestOutfitAsync(
            wardrobeItems,
            request.Occasion,
            request.Weather
        );

        // Get or create user
        var userGuid = await GetOrCreateUserGuidAsync(userId);

        // Extract item IDs from OutfitItems collection
        var topItem = suggestion.OutfitItems?.FirstOrDefault(oi => oi.ItemRole == "Top");
        var bottomItem = suggestion.OutfitItems?.FirstOrDefault(oi => oi.ItemRole == "Bottom");
        var shoeItem = suggestion.OutfitItems?.FirstOrDefault(oi => oi.ItemRole == "Shoes");

        // Create outfit entity
        var outfit = new OutfitSuggestion
        {
            UserId = userGuid,
            Occasion = request.Occasion,
            Weather = request.Weather,
            Reasoning = suggestion.Reasoning,
            StyleTip = suggestion.StyleTip,
            OutfitItems = []
        };

        // Add outfit items
        if (topItem != null)
        {
            outfit.OutfitItems.Add(new OutfitItem
            {
                WardrobeItemId = topItem.WardrobeItemId,
                ItemRole = "Top"
            });
        }
        if (bottomItem != null)
        {
            outfit.OutfitItems.Add(new OutfitItem
            {
                WardrobeItemId = bottomItem.WardrobeItemId,
                ItemRole = "Bottom"
            });
        }
        if (shoeItem != null)
        {
            outfit.OutfitItems.Add(new OutfitItem
            {
                WardrobeItemId = shoeItem.WardrobeItemId,
                ItemRole = "Shoes"
            });
        }

        // Persist to database
        var savedOutfit = await _outfitRepository.CreateAsync(outfit);

        return new OutfitSuggestionResponse
        {
            Id = savedOutfit.Id.ToString(),
            TopId = topItem?.WardrobeItemId.ToString(),
            BottomId = bottomItem?.WardrobeItemId.ToString(),
            ShoeId = shoeItem?.WardrobeItemId.ToString(),
            Occasion = request.Occasion,
            Weather = request.Weather,
            Reasoning = suggestion.Reasoning,
            StyleTip = suggestion.StyleTip
        };
    }

    public async Task<IEnumerable<OutfitSuggestionResponse>> GetSavedOutfitsAsync(string userId)
    {
        var userGuid = await GetOrCreateUserGuidAsync(userId);
        var outfits = await _outfitRepository.GetByUserIdAsync(userGuid);

        return outfits.Select(o =>
        {
            var topItem = o.OutfitItems?.FirstOrDefault(oi => oi.ItemRole == "Top");
            var bottomItem = o.OutfitItems?.FirstOrDefault(oi => oi.ItemRole == "Bottom");
            var shoeItem = o.OutfitItems?.FirstOrDefault(oi => oi.ItemRole == "Shoes");

            return new OutfitSuggestionResponse
            {
                Id = o.Id.ToString(),
                TopId = topItem?.WardrobeItemId.ToString(),
                BottomId = bottomItem?.WardrobeItemId.ToString(),
                ShoeId = shoeItem?.WardrobeItemId.ToString(),
                Occasion = o.Occasion,
                Weather = o.Weather,
                Reasoning = o.Reasoning,
                StyleTip = o.StyleTip
            };
        });
    }

    public async Task<OutfitSuggestionResponse> SaveOutfitAsync(string userId, OutfitSuggestionResponse outfitDto)
    {
        var userGuid = await GetOrCreateUserGuidAsync(userId);

        var outfit = new OutfitSuggestion
        {
            UserId = userGuid,
            Occasion = outfitDto.Occasion ?? "",
            Weather = outfitDto.Weather ?? "",
            Reasoning = outfitDto.Reasoning ?? "",
            StyleTip = outfitDto.StyleTip ?? "",
            IsFavorite = true,
            OutfitItems = []
        };

        if (!string.IsNullOrEmpty(outfitDto.TopId) && Guid.TryParse(outfitDto.TopId, out var topGuid))
        {
            outfit.OutfitItems.Add(new OutfitItem { WardrobeItemId = topGuid, ItemRole = "Top" });
        }
        if (!string.IsNullOrEmpty(outfitDto.BottomId) && Guid.TryParse(outfitDto.BottomId, out var bottomGuid))
        {
            outfit.OutfitItems.Add(new OutfitItem { WardrobeItemId = bottomGuid, ItemRole = "Bottom" });
        }
        if (!string.IsNullOrEmpty(outfitDto.ShoeId) && Guid.TryParse(outfitDto.ShoeId, out var shoeGuid))
        {
            outfit.OutfitItems.Add(new OutfitItem { WardrobeItemId = shoeGuid, ItemRole = "Shoes" });
        }

        var saved = await _outfitRepository.CreateAsync(outfit);

        return new OutfitSuggestionResponse
        {
            Id = saved.Id.ToString(),
            TopId = outfitDto.TopId,
            BottomId = outfitDto.BottomId,
            ShoeId = outfitDto.ShoeId,
            Occasion = outfitDto.Occasion,
            Weather = outfitDto.Weather,
            Reasoning = outfitDto.Reasoning,
            StyleTip = outfitDto.StyleTip
        };
    }

    public async Task<bool> DeleteOutfitAsync(string userId, string outfitId)
    {
        if (!Guid.TryParse(outfitId, out var outfitGuid))
            return false;

        return await _outfitRepository.DeleteAsync(outfitGuid);
    }

    private async Task<Guid> GetOrCreateUserGuidAsync(string userId)
    {
        // Try to parse as GUID first
        if (Guid.TryParse(userId, out var userGuid))
        {
            var existingUser = await _userRepository.GetByIdAsync(userGuid);
            if (existingUser != null)
                return userGuid;
        }

        // Try to find by email (userId might be email)
        var userByEmail = await _userRepository.GetByEmailAsync(userId);
        if (userByEmail != null)
            return userByEmail.Id;

        // Create a default anonymous user for the session
        var anonymousEmail = $"anonymous-{userId}@stylora.local";
        var existingAnonymous = await _userRepository.GetByEmailAsync(anonymousEmail);
        if (existingAnonymous != null)
            return existingAnonymous.Id;

        var newUser = new User
        {
            Email = anonymousEmail,
            PasswordHash = "anonymous",
            DisplayName = "Anonymous User"
        };
        var created = await _userRepository.CreateAsync(newUser);
        return created.Id;
    }
}
