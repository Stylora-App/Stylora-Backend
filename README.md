# Stylora Backend

A .NET 9 Web API using Clean Architecture for the Stylora fashion application.

## Architecture

The backend follows Clean Architecture principles with the following layers:

```
Backend/
├── Stylora.Domain/           # Entities and core domain logic
├── Stylora.Application/      # Use cases, DTOs, and interfaces
├── Stylora.Infrastructure/   # External services (Gemini AI) and repositories
└── Stylora.API/              # Web API controllers and configuration
```

### Layers

1. **Stylora.Domain** - Contains entities like `WardrobeItem`, `UserProfile`, `SeasonAnalysisResult`, and `OutfitSuggestion`.

2. **Stylora.Application** - Contains:
   - DTOs for API requests/responses
   - Service interfaces (`IGeminiService`, `IWardrobeRepository`)
   - Application services (`AnalysisService`, `WardrobeService`, `TryOnService`, `OutfitService`)

3. **Stylora.Infrastructure** - Contains:
   - `GeminiService` - Integration with Google's Gemini AI API
   - `InMemoryWardrobeRepository` - In-memory data storage (can be replaced with database)

4. **Stylora.API** - Contains:
   - Controllers (`WardrobeController`, `AnalysisController`, `TryOnController`, `OutfitController`)
   - Dependency injection configuration
   - CORS configuration for Angular frontend

## Prerequisites

- .NET 9 SDK
- A Google Gemini API Key

## Configuration

1. Set your Gemini API key in `appsettings.json`:
```json
{
  "GeminiApiKey": "your-api-key-here"
}
```

Or set it as an environment variable:
```powershell
$env:GEMINI_API_KEY = "your-api-key-here"
```

## Running the Backend

```powershell
cd Backend
dotnet run --project Stylora.API
```

The API will start at `https://localhost:5001` and `http://localhost:5000`.

## API Endpoints

### Wardrobe
- `GET /api/wardrobe/items` - Get all wardrobe items
- `POST /api/wardrobe/items` - Add a new wardrobe item
- `DELETE /api/wardrobe/items/{id}` - Delete a wardrobe item
- `POST /api/wardrobe/items/{id}/wear` - Log wearing an item
- `GET /api/wardrobe/profile` - Get user profile
- `PUT /api/wardrobe/profile` - Update user profile

### Analysis
- `POST /api/analysis/season` - Analyze season/color palette from photo
- `POST /api/analysis/save-profile` - Save analysis results to profile

### Try-On
- `POST /api/tryon/generate` - Generate virtual try-on image

### Outfit
- `POST /api/outfit/suggest` - Get outfit suggestion based on wardrobe items

## Development

To build the solution:
```powershell
cd Backend
dotnet build
```

To run tests (if available):
```powershell
dotnet test
```
