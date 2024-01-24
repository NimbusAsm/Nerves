using Microsoft.AspNetCore.Mvc;
using Nerves.Shared.Models;

namespace Nerves.ApiServer.Controllers;

[ApiController]
[Route("Api/[controller]")]
[ApiExplorerSettings(GroupName = "V1")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger)
    {
        _logger = logger;
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("{id}", Name = nameof(GetUserById))]
    public User GetUserById(int? id = 0, bool containSecurityInfo = false)
    {
        return new User()
        {
            Id = id,
            Name = "Dynesshely",
            JoinTime = DateTime.Parse("2021-08-01"),
            LastLoginTime = DateTime.Now,
            Data = new()
            {

            },
            SecurityInfo = containSecurityInfo ? new()
            {
                VerifiedEmail = new()
                {
                    $"Dynesshely@catrol.email"
                },
                VerifiedPhoneNumber = new()
                {
                    "12344445555"
                },
                VerifiedDevices = new()
                {
                    new()
                    {
                        Name = "DESKTOP-MAIN",
                        OS = "Windows 11",
                        OSVersion = "22H2",
                        Type = "Desktop Computer",
                        TokenHash = "111".GetHashCode().ToString(),
                        LastLoginTime = DateTime.Now,
                        MacAddress = "sss",
                        Ip = "4.124.53.12",
                        IsOnline = true
                    }
                },
                UserTokenHash = "sdfa".GetHashCode().ToString()
            } : null,
            IsOnline = true
        };
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("GetUsers/{ids}", Name = nameof(GetUsersByIds))]
    public IEnumerable<User> GetUsersByIds(List<int>? ids)
    {
        return new List<User>()
        {

        }.ToArray();
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpPost("Update/{id}", Name = nameof(UpdateUser))]
    public int UpdateUser(int? id, [FromBody] User user)
    {

        return 0;
    }

    [ApiExplorerSettings(GroupName = "V1")]
    [HttpDelete("Delete/{id}", Name = nameof(DeleteUser))]
    public bool DeleteUser(int? id)
    {

        return true;
    }
}
