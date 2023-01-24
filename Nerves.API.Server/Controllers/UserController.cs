using Microsoft.AspNetCore.Mvc;
using Nerves.API.Server.Models;

namespace Nerves.API.Server.Controllers;

/// <summary>
/// 用户数据控制器
/// </summary>
[ApiController]
[Route("Api/[controller]")]
[ApiExplorerSettings(GroupName = "V1")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;

    /// <summary>
    /// 用户数据控制器构造函数
    /// </summary>
    /// <param name="logger"></param>
    public UserController(ILogger<UserController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 通过 ID 获取用户实体数据
    /// </summary>
    /// <param name="id">ID</param>
    /// <param name="containSecurityInfo">是否包含安全信息实体</param>
    /// <returns>用户实体数据</returns>
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

    /// <summary>
    /// 获取一组用户实体数据
    /// </summary>
    /// <param name="ids">ID 列表</param>
    /// <returns>一组用户实体数据</returns>
    [ApiExplorerSettings(GroupName = "V1")]
    [HttpGet("GetUsers/{ids}", Name = nameof(GetUsersByIds))]
    public IEnumerable<User> GetUsersByIds(List<int>? ids)
    {
        return new List<User>()
        {
            
        }.ToArray();
    }

    /// <summary>
    /// 更新用户实体数据
    /// </summary>
    /// <param name="id">ID</param>
    /// <param name="user">新的用户数据</param>
    /// <returns>状态码</returns>
    [ApiExplorerSettings(GroupName = "V1")]
    [HttpPost("Update/{id}", Name = nameof(UpdateUser))]
    public int UpdateUser(int? id, [FromBody] User user)
    {

        return 0;
    }

    /// <summary>
    /// 删除用户实体数据
    /// </summary>
    /// <param name="id"></param>
    /// <returns>删除是否成功</returns>
    [ApiExplorerSettings(GroupName = "V1")]
    [HttpDelete("Delete/{id}", Name = nameof(DeleteUser))]
    public bool DeleteUser(int? id)
    {

        return true;
    }
}
