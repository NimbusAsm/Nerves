using Nerves.ApiServer.Models;

namespace Nerves.ApiServer.Services;

/// <summary>
/// 用户管理器
/// </summary>
public class UserManager
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public UserManager()
    {
        
    }

    /// <summary>
    /// 设置数据源
    /// </summary>
    /// <returns>用户管理器实例</returns>
    public UserManager SetDataSource()
    {
        return this;
    }

    /// <summary>
    /// 添加新用户
    /// </summary>
    /// <param name="user">新用户信息</param>
    /// <returns>成功放回用户 ID, 失败返回 -1</returns>
    public int AppendUser(User user)
    {
        return 0;
    }

    /// <summary>
    /// 获取单个用户信息
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>用户信息</returns>
    public User? GetUser(int id)
    {
        return default;
    }

    /// <summary>
    /// 获取部分用户信息
    /// </summary>
    /// <param name="startId">从哪个 ID 开始获取</param>
    /// <param name="count">向后获取多少个用户信息</param>
    /// <returns>用户信息列表</returns>
    public IEnumerable<User> GetUsers(int startId, int count = 1)
    {
        return new List<User>();
    }

    /// <summary>
    /// 更新单个用户信息
    /// </summary>
    /// <param name="id">ID</param>
    /// <param name="user">新的用户信息</param>
    /// <returns>是否更新成功</returns>
    public bool UpdateUser(int id, User user)
    {
        return true;
    }

    /// <summary>
    /// 删除单个用户
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>是否删除成功</returns>
    public bool DeleteUser(int id)
    {
        return true;
    }
}
