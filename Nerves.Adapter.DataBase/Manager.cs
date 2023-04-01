using MongoDB.Driver;

namespace Nerves.Adapter.DataBase
{
    public class Manager
    {

        private string connectionString = "";
        private MongoClient? client;

        /// <summary>
        /// 更新连接字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns>管理器本身</returns>
        public Manager UpdateConnectionString(string str)
        {
            connectionString = str;
            return this;
        }

        public async Task<Manager> Connect()
        {
            if (client is null)
            {
                await Task.Run(() => client = new MongoClient(connectionString));
            }
            else
            {

            }
            return this;
        }
    }
}
