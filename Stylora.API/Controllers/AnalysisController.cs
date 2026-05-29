using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.Exceptions;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalysisController : BaseApiController
{
    private readonly IAnalysisService _analysisService;

    public AnalysisController(IAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>Analyse a face photo to determine colour season.</summary>
    /// <remarks>Calls Google Gemini Vision to classify the user's season, palette, undertone, and contrast.</remarks>
    [HttpPost("season")]
    [ProducesResponseType(typeof(SeasonAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SeasonAnalysisResponse>> AnalyzeSeason([FromBody] SeasonAnalysisRequest request)
    {
        try
        {
            var result = await _analysisService.AnalyzeSeasonAsync(GetUserId(), request);
            return Ok(result);
        }
        catch (ExternalServiceException ex)
        {
            return StatusCode((int)ex.StatusCode, new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "Failed to analyze season. Please try again." });
        }
    }

    /// <summary>Get the user's most recent season analysis result.</summary>
    /// <remarks>Returns null if the user has not completed an analysis yet.</remarks>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(SeasonAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SeasonAnalysisResponse?>> GetLatestAnalysis()
    {
        try
        {
            var result = await _analysisService.GetLatestAnalysisAsync(GetUserId());
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Save a season analysis result to the user's profile.</summary>
    [HttpPost("save-profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> SaveSeasonProfile([FromBody] SeasonAnalysisResponse analysis)
    {
        try
        {
            var profile = await _analysisService.SaveSeasonProfileAsync(GetUserId(), analysis);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
