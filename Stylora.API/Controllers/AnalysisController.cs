using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisService _analysisService;

    public AnalysisController(AnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    private const string DefaultUserId = "default-user";

    [HttpPost("season")]
    public async Task<ActionResult<SeasonAnalysisResponse>> AnalyzeSeason([FromBody] SeasonAnalysisRequest request)
    {
        try
        {
            var result = await _analysisService.AnalyzeSeasonAsync(request);
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
            var profile = await _analysisService.SaveSeasonProfileAsync(DefaultUserId, analysis);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
