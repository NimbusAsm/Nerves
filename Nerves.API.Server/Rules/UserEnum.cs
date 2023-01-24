namespace Nerves.API.Server.Rules;

/// <summary>
/// 用户相关枚举值
/// </summary>
public class UserEnum
{
    /// <summary>
    /// 用户性别
    /// </summary>
    public enum Sex
    {
        /// <summary>
        /// 未知
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 保密
        /// </summary>
        Secret = 1,

        /// <summary>
        /// 男性
        /// </summary>
        Male = 2,

        /// <summary>
        /// 女性
        /// </summary>
        Female = 3,
    }

}
