using Nerves.Shared.Models;

namespace Nerves.ApiServer.Services;

public class UserManager
{
    public UserManager()
    {

    }

    public UserManager SetDataSource()
    {
        return this;
    }

    public int AppendUser(User user)
    {
        return 0;
    }

    public User? GetUser(int id)
    {
        return default;
    }

    public IEnumerable<User> GetUsers(int startId, int count = 1)
    {
        return new List<User>();
    }

    public bool UpdateUser(int id, User user)
    {
        return true;
    }

    public bool DeleteUser(int id)
    {
        return true;
    }
}
