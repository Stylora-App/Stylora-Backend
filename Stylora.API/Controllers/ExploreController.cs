using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExploreController : BaseApiController
{
    private readonly IExploreService _exploreService;

    public ExploreController(IExploreService exploreService)
    {
        _exploreService = exploreService;
    }

    /// <summary>
    /// Search ASOS products filtered and sorted by the user's colour palette.
    /// </summary>
    /// <param name="q">Free-text search query (optional; falls back to season-based terms).</param>
    /// <param name="category">Category slug: all | tops | bottoms | dresses | shoes | accessories | outerwear</param>
    /// <param name="season">User's season (e.g. "True Autumn") used to build the default query.</param>
    /// <param name="subSeason">User's sub-season (e.g. "Deep Autumn") used to resolve the canonical palette vector.</param>
    /// <param name="palette">Comma-separated hex colour codes from the user's palette (e.g. "#FF5733,#C19A6B").</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, 1–48 (default 20).</param>
    [HttpGet]
    public async Task<ActionResult<ExploreResultDto>> Search(
        [FromQuery] string?  q        = null,
        [FromQuery] string?  category = "all",
        [FromQuery] string?  gender   = null,
        [FromQuery] string?  season   = null,
        [FromQuery] string?  subSeason = null,
        [FromQuery] string?  palette  = null,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 20)
    {
        var paletteList = string.IsNullOrWhiteSpace(palette)
            ? []
            : palette.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .ToList();

        var query = new ExploreQueryDto
        {
            Q        = q,
            Category = category,
            Gender   = gender,
            Season   = season,
            SubSeason = subSeason,
            Palette  = paletteList,
            Page     = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 48),
        };

        try
        {
            var result = await _exploreService.SearchAsync(query);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("ASOS API error"))
        {
            return StatusCode(502, new { message = "Failed to fetch products from ASOS. Please try again." });
        }
    }
}
