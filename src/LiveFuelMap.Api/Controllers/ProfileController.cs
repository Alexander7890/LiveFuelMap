using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveFuelMap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(IProfileService profileService, IWebHostEnvironment environment) : ControllerBase
{
    private static readonly Dictionary<string, string> AllowedProfileImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        return Ok(await profileService.GetAsync(userId, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            return Ok(await profileService.UpdateAsync(userId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> UploadPhoto([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        try
        {
            if (file is null)
                return BadRequest(new { error = "Profile image is required." });

            if (file.Length <= 0)
                return BadRequest(new { error = "Profile image is empty." });

            if (file.Length > 2_000_000)
                return BadRequest(new { error = "Profile image must be 2 MB or smaller." });

            if (!AllowedProfileImageTypes.TryGetValue(file.ContentType, out var extension))
                return BadRequest(new { error = "Profile image must be JPG, PNG, WEBP or GIF." });

            var uploadsPath = Path.GetFullPath(Path.Combine(
                environment.ContentRootPath,
                "..",
                "..",
                "frontend",
                "public",
                "uploads",
                "profiles"));

            Directory.CreateDirectory(uploadsPath);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(uploadsPath, fileName);

            await using (var stream = System.IO.File.Create(physicalPath))
                await file.CopyToAsync(stream, cancellationToken);

            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var imageUrl = $"/uploads/profiles/{fileName}";
            return Ok(await profileService.UpdateAsync(userId, new UpdateProfileRequest(null, null, imageUrl), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await profileService.DeleteAsync(userId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
