using Microsoft.AspNetCore.Mvc;
using Nerves.ApiServer.Utils;
using Nerves.Shared.Options.DataBaseOptions;
using Nerves.Shared.Models.User;

namespace Nerves.ApiServer.Controllers;

[ApiController]
[Route("Api/[controller]")]
[ApiExplorerSettings(GroupName = "V1")]
public class UserController(ILogger<UserController> logger) : ControllerBase
{
    private readonly ILogger<UserController> _logger = logger;

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("{name}", Name = nameof(GetUserByName))]
    public async Task<IActionResult> GetUserByName(string name, [FromQuery] string? token, [FromQuery] string? deviceId)
    {
        if (await Instances.userManager!.CheckToken(name, token, deviceId) == false)
            return BadRequest();

        var queriedUser = await Instances.userManager!.GetUserByNameAsync(name);

        if (queriedUser is null)
            return NotFound();
        else return Ok(queriedUser);
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpPost("Create", Name = nameof(CreateUser))]
    public IActionResult CreateUser([FromBody] User user)
    {
        try
        {
            Instances.userManager!.InsertUserAsync(user, new()
            {
                ActionWhenExists = AlreadyExistsActions.ThrowException
            });
        }
        catch (Exception e)
        {
            return BadRequest(e);
        }

        return Ok(0);
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpPost("Update/{name}", Name = nameof(UpdateUser))]
    public async Task<IActionResult> UpdateUser(
        string name,
        [FromBody] User user,
        [FromQuery] string? token,
        [FromQuery] string? deviceId)
    {
        if (await Instances.userManager!.CheckToken(name, token, deviceId) == false)
            return BadRequest();

        Instances.userManager!.InsertUserAsync(user, new()
        {
            ActionWhenExists = AlreadyExistsActions.Replace
        });

        return Ok(0);
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("Update/{name}", Name = nameof(UpdateUserPassword))]
    public async Task<IActionResult> UpdateUserPassword(
        string name,
        [FromQuery] string? token,
        [FromQuery] string? deviceId,
        [FromQuery] string new_passwd)
    {
        if (await Instances.userManager!.CheckToken(name, token, deviceId) == false)
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
    [HttpDelete("Delete/{name}", Name = nameof(DeleteUser))]
    public async Task<IActionResult> DeleteUser(string name, [FromQuery] string? token, [FromQuery] string? deviceId)
    {
        if (await Instances.userManager!.CheckToken(name, token, deviceId) == false)
            return BadRequest();

        var result = await Instances.userManager!.DeleteUserAsync(name);
        return Ok(result.DeletedCount);
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("Login/{name}", Name = nameof(Login))]
    public async Task<IActionResult> Login(string name, [FromQuery] string? password, [FromQuery] string? deviceId)
    {
        if (password is null || deviceId is null)
            return BadRequest();

        var queriedUser = await Instances.userManager!.GetUserByNameAsync(name);

        if (queriedUser is null)
            return NotFound();
        else if (UserUtil.VerifyPassword(password, queriedUser))
        {
            var token = await Instances.userManager!.GetOneTokenAsync(name, deviceId);
            return Ok(token);
        }
        else return NotFound();
    }
}
