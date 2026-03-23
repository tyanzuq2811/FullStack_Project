using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace IPM.Infrastructure.Identity;

/// <summary>
/// Custom password hasher using Argon2id algorithm
/// OWASP recommended parameters: m=47104 (46 MiB), t=1, p=1
/// </summary>
public class Argon2idPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySize = 47104; // 46 MiB in KB
    private const int Iterations = 1;
    private const int Parallelism = 1;

    public string HashPassword(TUser user, string password)
    {
        var salt = GenerateSalt();
        var hash = HashPasswordWithArgon2id(password, salt);

        // Return in PHC string format: $argon2id$v=19$m=47104,t=1,p=1$salt$hash
        var saltBase64 = Convert.ToBase64String(salt);
        var hashBase64 = Convert.ToBase64String(hash);

        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={Parallelism}${saltBase64}${hashBase64}";
    }

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        try
        {
            // Parse PHC format
            var parts = hashedPassword.Split('$');
            if (parts.Length != 6 || parts[1] != "argon2id")
            {
                // Fallback: might be old PBKDF2 hash, rehash needed
                return PasswordVerificationResult.Failed;
            }

            var parameters = ParseParameters(parts[3]);
            var salt = Convert.FromBase64String(parts[4]);
            var expectedHash = Convert.FromBase64String(parts[5]);

            var actualHash = HashPasswordWithArgon2id(
                providedPassword,
                salt,
                parameters.memory,
                parameters.iterations,
                parameters.parallelism
            );

            if (CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            {
                // Check if parameters need upgrade
                if (parameters.memory < MemorySize || parameters.iterations < Iterations)
                {
                    return PasswordVerificationResult.SuccessRehashNeeded;
                }
                return PasswordVerificationResult.Success;
            }

            return PasswordVerificationResult.Failed;
        }
        catch
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static byte[] GenerateSalt()
    {
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }

    private static byte[] HashPasswordWithArgon2id(
        string password,
        byte[] salt,
        int memorySize = MemorySize,
        int iterations = Iterations,
        int parallelism = Parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memorySize,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(HashSize);
    }

    private static (int memory, int iterations, int parallelism) ParseParameters(string paramString)
    {
        int memory = MemorySize;
        int iterations = Iterations;
        int parallelism = Parallelism;

        var pairs = paramString.Split(',');
        foreach (var pair in pairs)
        {
            var kv = pair.Split('=');
            if (kv.Length == 2)
            {
                switch (kv[0])
                {
                    case "m":
                        memory = int.Parse(kv[1]);
                        break;
                    case "t":
                        iterations = int.Parse(kv[1]);
                        break;
                    case "p":
                        parallelism = int.Parse(kv[1]);
                        break;
                }
            }
        }

        return (memory, iterations, parallelism);
    }
}
