using System.Collections.Generic;

namespace Application.VaccineCredential.Queries.GetVaccineCredential
{
    public class Name
    {
        public string Family { get; set; }
        public List<string> Given { get; set; }
    }

    public class Coding
    {
        public string System { get; set; }
        public string Code { get; set; }
    }

    public class VaccineCode
    {
        public List<Coding> Coding { get; set; }
    }

    public class Patient
    {
        public string Reference { get; set; }
    }

    public class Actor
    {
        public string Display { get; set; }
    }

    public class Performer
    {
        public Actor Actor { get; set; }
    }

    public class Resource
    {
        public string ResourceType { get; set; }
        public List<Name> Name { get; set; }
        public string BirthDate { get; set; }
        public string Status { get; set; }
        public VaccineCode VaccineCode { get; set; }
        public Patient Patient { get; set; }
        public string OccurrenceDateTime { get; set; }
        public string LotNumber { get; set; }
        public List<Performer> Performer { get; set; }
    }

    public class Entry
    {
        public string FullUrl { get; set; }
        public Resource Resource { get; set; }
    }

    public class FhirBundle
    {
        public string ResourceType { get; set; }
        public string Type { get; set; }
        public List<Entry> Entry { get; set; }
    }

    public class CredentialSubject
    {
        public string FhirVersion { get; set; }
        public FhirBundle FhirBundle { get; set; }
    }

    public class Vc
    {
        public List<string> Type { get; set; }
        public CredentialSubject CredentialSubject { get; set; }
    }

    public class Vci
    {
        public string Iss { get; set; }
        public long Nbf { get; set; }
        public Vc Vc { get; set; }
    }

    public class VerifiableCredentials
    {
        public List<string> VerifiableCredential { get; set; }
    }
}