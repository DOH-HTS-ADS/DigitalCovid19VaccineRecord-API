using Application.Common.Models;

namespace Application.VaccineCredential.Queries.GetVaccineStatus
{
    public class StatusModel
    {
        public bool ViewStatus { get; set; }
        public bool InvalidPin { get; set; }
        public RateLimit RateLimit { get; set; }
    }
}