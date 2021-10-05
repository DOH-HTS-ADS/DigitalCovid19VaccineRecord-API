using Application.Common.Models;
using System;

namespace Application.Common.Interfaces
{
    public interface IJwtSign
    {
        string Signature(byte[] payload);

        long ToUnixTimestamp(DateTime date);

        Thumbprint GetThumbprint(string certificate);

        string GetKid(Thumbprint thumbprint);

        string SignWithRsaKey(byte[] payload);
    }
}