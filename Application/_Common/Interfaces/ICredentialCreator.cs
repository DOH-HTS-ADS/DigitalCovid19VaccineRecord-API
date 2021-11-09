using Application.VaccineCredential.Queries.GetVaccineCredential;

namespace Application.Common.Interfaces
{
    public interface ICredentialCreator
    {
        Vci GetCredential(Vc vc);

        GoogleWallet GetGoogleCredential(Vci data, string shc);
    }
}