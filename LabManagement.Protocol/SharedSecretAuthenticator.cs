using System.Security.Cryptography;
using System.Text;

namespace LabManagement.Protocol;

public static class SharedSecretAuthenticator
{
    public static string CreateChallenge() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string CreateProof(
        string sharedSecret,
        string challenge,
        string hostname)
    {
        byte[] key = Convert.FromBase64String(sharedSecret);
        byte[] message = Encoding.UTF8.GetBytes(
            $"labmanagement-v1|{hostname}|{challenge}");
        return Convert.ToBase64String(HMACSHA256.HashData(key, message));
    }

    public static bool VerifyProof(
        string sharedSecret,
        string challenge,
        string hostname,
        string proof)
    {
        try
        {
            byte[] expected = Convert.FromBase64String(
                CreateProof(sharedSecret, challenge, hostname));
            byte[] received = Convert.FromBase64String(proof);
            return CryptographicOperations.FixedTimeEquals(expected, received);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
