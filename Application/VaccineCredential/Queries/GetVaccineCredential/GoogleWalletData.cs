using System.Collections.Generic;

namespace Application.VaccineCredential.Queries.GetVaccineCredential
{
    public enum BarcodeType
    {
        BARCODE_TYPE_UNSPECIFIED,
        AZTEC,
        CODE_39,
        CODE_128,
        CODABAR,
        DATA_MATRIX,
        EAN_8,
        EAN_13,
        ITF_14,
        PDF_417,
        QR_CODE,
        UPC_A,
        TEXT_ONLY
    }

    public enum BarcodeRenderEncoding
    {
        RENDER_ENCODING_UNSPECIFIED,
        UTF_8
    }

    public class TranslatedString
    {
        public string Language { get; set; }
        public string Value { get; set; }
    }

    public class LocalizedString
    {
        public List<TranslatedString> TranslatedValues { get; set; }
        public TranslatedString DefaultValue { get; set; }
    }

    public class Barcode
    {
        public string AlternateText { get; set; }
        public LocalizedString ShowCodeText { get; set; }
        public string Type { get; set; }

        public string RenderEncoding { get; set; }
        public string Value { get; set; }
    }

    public class SourceUri
    {
        public string Description { get; set; }
        public string Uri { get; set; }
    }

    public class Logo
    {
        public SourceUri SourceUri { get; set; }
    }

    public class PatientDetails
    {
        public string DateOfBirth { get; set; }
        public string IdentityAssuranceLevel { get; set; }
        public string PatientId { get; set; }
        public string PatientName { get; set; }
    }

    public class VaccinationRecord
    {
        public string Code { get; set; }
        public string ContactInfo { get; set; }
        public string Description { get; set; }
        public string DoseDateTime { get; set; }
        public string DoseLabel { get; set; }
        public string LotNumber { get; set; }
        public string Manufacturer { get; set; }
        public string Provider { get; set; }
    }

    public class VaccinationDetails
    {
        public List<VaccinationRecord> VaccinationRecord { get; set; }
    }

    public class CovidCardObject
    {
        public string Id { get; set; }
        public string IssuerId { get; set; }
        public Barcode Barcode { get; set; }
        public string CardColorHex { get; set; }
        public string Expiration { get; set; }
        public Logo Logo { get; set; }
        public PatientDetails PatientDetails { get; set; }
        public string Title { get; set; }
        public VaccinationDetails VaccinationDetails { get; set; }
    }

    public class Payload
    {
        public List<CovidCardObject> CovidCardObjects { get; set; }
    }

    public class GoogleWallet
    {
        public string Iss { get; set; }
        public string Aud { get; set; }
        public long Iat { get; set; }
        public string Typ { get; set; }
        public List<object> Origins { get; set; }
        public Payload Payload { get; set; }
    }
}