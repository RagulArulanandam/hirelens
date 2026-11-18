using System.Security.Claims;
using FluentValidation;
using HireLens.Api.Services;
using HireLens.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Api.Controllers;

public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, DateTime Expires, string Email, string FullName, string[] Roles);

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress(); RuleFor(x => x.Password).NotEmpty(); }
}

[ApiController, Route("api/auth")]
public class AuthController(UserManager<AppUser> users, JwtService jwt, IValidator<LoginRequest> v) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        await v.ValidateAndThrowAsync(req);
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null || await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, req.Password))
        {
            if (user is not null) await users.AccessFailedAsync(user);
            return Problem(statusCode: 401, title: "Invalid credentials");
        }
        await users.ResetAccessFailedCountAsync(user);
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var (token, exp) = jwt.Create(user, roles);
        return new AuthResponse(token, exp, user.Email!, user.FullName, roles);
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user is null) return Unauthorized();
        var roles = (await users.GetRolesAsync(user)).ToArray();
        return new AuthResponse("", DateTime.UtcNow, user.Email!, user.FullName, roles);
    }
}
