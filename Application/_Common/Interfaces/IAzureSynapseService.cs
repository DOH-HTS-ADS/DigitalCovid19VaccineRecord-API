using Application.VaccineCredential.Queries.GetVaccineCredential;
using Application.VaccineCredential.Queries.GetVaccineStatus;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IAzureSynapseService
    {
        Task<Vc> GetVaccineCredentialSubjectAsync(string id, string middleName, string auditId, CancellationToken cancellationToken);

        Task<string> GetVaccineCredentialStatusAsync(GetVaccineCredentialStatusQuery request, CancellationToken cancellationToken);

        //Task<string> GetVaccineCredentialStatusAsync(SqlConnection conn, GetVaccineCredentialStatusQuery request, CancellationToken cancellationToken);
    }
}