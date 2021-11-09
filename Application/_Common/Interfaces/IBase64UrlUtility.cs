namespace Application.Common.Interfaces
{
    public interface IBase64UrlUtility
    {
        string Encode(byte[] arg);

        byte[] Decode(string arg);
    }
}