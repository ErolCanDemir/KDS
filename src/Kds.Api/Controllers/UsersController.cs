using Kds.Contracts.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

[ApiController]
[Route("api/kullanicilar")]
[Authorize]
public sealed class UsersController(ISender gonderici) : ControllerBase
{
    /// <summary>Mevcut tenant'ın kullanıcıları (JIT ile giriş yapmış olanlar).</summary>
    [HttpGet]
    public Task<IReadOnlyList<UserSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListUsersRequest(), cancellationToken);
}
