using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ExcelToolStack.Web.Services;

public sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(AnonymousState);
}
