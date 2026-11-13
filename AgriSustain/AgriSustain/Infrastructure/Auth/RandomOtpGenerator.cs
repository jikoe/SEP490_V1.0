using System.Security.Cryptography;
using AgriSustain.Application.Interfaces.Services;

namespace AgriSustain.Infrastructure.Auth;

/// <summary>Cryptographically random 6-digit OTP.</summary>
public sealed class RandomOtpGenerator : IOtpGenerator
{
    /// <summary>
    /// Cryptographically random number 0–999999 formatted with leading zeros.
    /// </summary>
    /// <returns>Six digits.</returns>
    public string Generate() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
