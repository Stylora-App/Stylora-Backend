using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalysisController : BaseApiController
{
    private readonly AnalysisService _analysisService;

    public AnalysisController(AnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [HttpPost("season")]
    public async Task<ActionResult<SeasonAnalysisResponse>> AnalyzeSeason([FromBody] SeasonAnalysisRequest request)
    {
        try
        {
            var result = await _analysisService.AnalyzeSeasonAsync(GetUserId(), request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<SeasonAnalysisResponse>>> GetAnalysisHistory()
    {
        try
        {
            var results = await _analysisService.GetAnalysisHistoryAsync(GetUserId());
            return Ok(results);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("latest")]
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

    [HttpPost("save-profile")]
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
