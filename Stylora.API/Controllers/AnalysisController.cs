using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
