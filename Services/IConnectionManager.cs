using SqlSugar;

namespace DynamicDbApi.Services
{
    public interface IConnectionManager
    {
        ISqlSugarClient GetConnection(string connectionId);
    }
}