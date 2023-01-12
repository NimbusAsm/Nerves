using Microsoft.AspNetCore.Mvc;
using Nerves.API.Server.Models;

namespace Nerves.API.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetUserByName")]
    public User Get(string? name = "test")
    {
        return new User()
        {
            Name = "Dynesshely",
            DisplayName = "常青园晚",
            JoinTime = DateTime.Parse("2021-08-01"),
            LastLoginTime = DateTime.Now,
            SecurityInfo = new()
            {
                VerifiedEmail = new()
                {
                    "catrol@qq.com"
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
            },
            IsOnline = true
        };
    }
}