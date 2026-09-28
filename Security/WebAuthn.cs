using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SaccoManagementSystem.Security;

public sealed record RegisteredCredential(byte[] CredentialId, byte[] PublicKeySpki, int Algorithm, long SignCount);

/// <summary>
/// Minimal WebAuthn relying-party verification: attestation "none", ES256 (-7) and RS256 (-257),
/// user verification required. No third-party packages.
/// </summary>
public static class WebAuthn
{
    public static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromB64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }

    public static string NewChallenge() => B64Url(RandomNumberGenerator.GetBytes(32));

    private static void CheckClientData(byte[] clientDataJson, string type, string expectedChallenge, string expectedOrigin)
    {
        using var doc = JsonDocument.Parse(clientDataJson);
        var root = doc.RootElement;
        if (root.GetProperty("type").GetString() != type) throw new SecurityException("Wrong ceremony type.");
        if (root.GetProperty("challenge").GetString() != expectedChallenge) throw new SecurityException("Challenge mismatch.");
        if (!string.Equals(root.GetProperty("origin").GetString(), expectedOrigin, StringComparison.OrdinalIgnoreCase))
            throw new SecurityException("Origin mismatch.");
    }

    private static void CheckAuthData(byte[] authData, string rpId, bool needAttested)
    {
        if (authData.Length < 37) throw new SecurityException("Authenticator data too short.");
        var rpHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        if (!CryptographicOperations.FixedTimeEquals(authData.AsSpan(0, 32), rpHash)) throw new SecurityException("RP ID mismatch.");
        var flags = authData[32];
        if ((flags & 0x01) == 0) throw new SecurityException("User presence required.");
        if ((flags & 0x04) == 0) throw new SecurityException("User verification required.");
        if (needAttested && (flags & 0x40) == 0) throw new SecurityException("No credential data.");
    }

    public static RegisteredCredential VerifyRegistration(byte[] clientDataJson, byte[] attestationObject,
        string expectedChallenge, string expectedOrigin, string rpId)
    {
        CheckClientData(clientDataJson, "webauthn.create", expectedChallenge, expectedOrigin);

        var att = Cbor.Map(Cbor.Decode(attestationObject));
        var authData = Cbor.Bytes(att["authData"]);
        CheckAuthData(authData, rpId, true);

        long signCount = ((long)authData[33] << 24) | ((long)authData[34] << 16) | ((long)authData[35] << 8) | authData[36];
        int p = 37 + 16; // skip AAGUID
        int credLen = (authData[p] << 8) | authData[p + 1];
        p += 2;
        var credId = authData.AsSpan(p, credLen).ToArray();
        p += credLen;

        var coseBytes = authData.AsSpan(p).ToArray();
        var cose = Cbor.Map(Cbor.Decode(coseBytes));
        long kty = Cbor.Int(cose[1L]);
        long alg = Cbor.Int(cose[3L]);

        byte[] spki;
        if (kty == 2 && alg == -7)
        {
            if (Cbor.Int(cose[-1L]) != 1) throw new SecurityException("Unsupported curve.");
            var ec = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Cbor.Bytes(cose[-2L]), Y = Cbor.Bytes(cose[-3L]) }
            });
            spki = ec.ExportSubjectPublicKeyInfo();
        }
        else if (kty == 3 && alg == -257)
        {
            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Modulus = Cbor.Bytes(cose[-1L]), Exponent = Cbor.Bytes(cose[-2L]) });
            spki = rsa.ExportSubjectPublicKeyInfo();
        }
        else throw new SecurityException("Unsupported key algorithm.");

        return new RegisteredCredential(credId, spki, (int)alg, signCount);
    }

    /// <summary>Verifies an assertion; returns the new signature counter.</summary>
    public static long VerifyAssertion(PasskeyRecord key, byte[] clientDataJson, byte[] authData, byte[] signature,
        string expectedChallenge, string expectedOrigin, string rpId)
    {
        CheckClientData(clientDataJson, "webauthn.get", expectedChallenge, expectedOrigin);
        CheckAuthData(authData, rpId, false);

        var signed = authData.Concat(SHA256.HashData(clientDataJson)).ToArray();
        bool ok;
        if (key.Algorithm == -7)
        {
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(key.PublicKeySpki, out _);
            ok = ec.VerifyData(signed, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        else if (key.Algorithm == -257)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(key.PublicKeySpki, out _);
            ok = rsa.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        else throw new SecurityException("Unsupported key algorithm.");

        if (!ok) throw new SecurityException("Bad signature.");

        long counter = ((long)authData[33] << 24) | ((long)authData[34] << 16) | ((long)authData[35] << 8) | authData[36];
        if (counter != 0 && key.SignCount != 0 && counter <= key.SignCount) throw new SecurityException("Signature counter went backwards (possible cloned key).");
        return counter;
    }
}

public sealed class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
}
