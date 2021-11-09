using Microsoft.Data.SqlClient;

namespace Application.Common.Models
{
    public class ConnectionThreadCount
    {
        public SqlConnection Connection { get; set; }
        public int TaskCount;
    }
}