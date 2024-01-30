using Microsoft.AspNetCore.Mvc;
using Nerves.ApiServer.Utils;
using Nerves.Shared.Configs.UsersConfigs.DataBaseOptions;
using Nerves.Shared.Models.User;

namespace Nerves.ApiServer.Controllers;

[ApiController]
[Route("Api/[controller]")]
[ApiExplorerSettings(GroupName = "V1")]
public class AdminUserController(ILogger<UserController> logger) : ControllerBase
{
    private readonly ILogger<UserController> _logger = logger;

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("{name}", Name = nameof(AdminGetUser))]
    public async Task<IActionResult> AdminGetUser(string name, [FromQuery] string? token)
    {
        if (token is null || await Instances.userManager!.CheckAdminToken(token) == false)
            return BadRequest();

        var user = await Instances.userManager!.GetUserByNameAsync(name);

        return user is null ? NotFound() : Ok(user);
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("Range", Name = nameof(AdminGetUsers))]
    public async Task<IActionResult> AdminGetUsers([FromQuery] int? startIndex, [FromQuery] int? endIndex, [FromQuery] string? token)
    {
        if (startIndex is null || endIndex is null)
            return BadRequest();

        if (token is null || await Instances.userManager!.CheckAdminToken(token) == false)
            return BadRequest();

        var users = Instances.userManager!.GetUsers(startIndex.Value, endIndex.Value);

        return Ok(users);
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpPost("Update", Name = nameof(AdminUpdateUser))]
    public async Task<IActionResult> AdminUpdateUser([FromBody] User user, [FromQuery] string? token)
    {
        if (token is null || await Instances.userManager!.CheckAdminToken(token) == false)
            return BadRequest();

        Instances.userManager!.InsertUserAsync(user, new()
        {
            ActionWhenExists = AlreadyExistsActions.Replace
        });

        return Ok(0);
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("Update/{name}", Name = nameof(AdminUpdateUserPassword))]
    public async Task<IActionResult> AdminUpdateUserPassword(string name, [FromQuery] string token, [FromQuery] string new_passwd)
    {
        if (token is null || await Instances.userManager!.CheckAdminToken(token) == false)
            return BadRequest();

        var user = (await Instances.userManager!.GetUserByNameAsync(name))!;

        UserUtil.UpdatePasswordHash(user, new_passwd);

        Instances.userManager!.InsertUserAsync(user, new()
        {
            ActionWhenExists = AlreadyExistsActions.Replace
        });

        _ = await Instances.userManager!.ClearToken(name);

        return Ok();
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpDelete("Delete/{name}", Name = nameof(AdminDeleteUser))]
    public async Task<IActionResult> AdminDeleteUser(string name, [FromQuery] string? token)
    {
        if (token is null || await Instances.userManager!.CheckAdminToken(token) == false)
            return BadRequest();

        _ = await Instances.userManager!.DeleteUserAsync(name);
        return NoContent();
    }
}
