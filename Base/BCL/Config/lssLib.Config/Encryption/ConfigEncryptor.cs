// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Encryption/ConfigEncryptor.cs
//  역할: AES-256-GCM 기반 설정값 암호화/복호화 헬퍼
// ══════════════════════════════════════════════════════════════════════════
using System.Security.Cryptography;
using System.Text;

namespace lssLib.Config.Encryption;

/// <summary>
/// AES-256-GCM 기반 설정값 암호화/복호화 정적 헬퍼.
/// </summary>
/// <remarks>
/// 암호문 포맷: <c>Base64(salt[16] + nonce[12] + tag[16] + ciphertext)</c>
/// <para>패스워드는 PBKDF2-SHA256(100,000회)으로 32바이트 키를 파생합니다.</para>
/// <para>암호화된 값은 파일에 <c>ENC:</c> 접두사를 붙여 저장됩니다.</para>
/// <example><code>
/// ConfigEncryptor.SetPassword("my-secret-pass");
///
/// string cipher = ConfigEncryptor.Encrypt("192.168.1.1");
/// string plain  = ConfigEncryptor.Decrypt(cipher);      // "192.168.1.1"
///
/// // 암호화 접두사 처리
/// string stored   = ConfigEncryptor.ToStoredValue("pass");   // "ENC:Base64..."
/// string restored = ConfigEncryptor.FromStoredValue(stored);  // "pass"
/// </code></example>
/// </remarks>
public static class ConfigEncryptor
{
    #region §1 ─ 상수

    /// <summary>암호화된 값의 파일 내 저장 접두사.</summary>
    public const string EncPrefix = "ENC:";

    private const int SaltSize = 16;
    private const int NonceSize = 12;   // AES-GCM 표준
    private const int TagSize = 16;   // AES-GCM 태그
    private const int KeySize = 32;   // AES-256
    private const int Iterations = 100_000;

    #endregion

    #region §2 ─ 패스워드 관리

    private static byte[]? _strKeyBytes;

    /// <summary>
    /// 암/복호화에 사용할 패스워드를 설정합니다.
    /// </summary>
    /// <param name="password">패스워드 문자열.</param>
    /// <exception cref="ArgumentException">빈 문자열인 경우.</exception>
    public static void SetPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("패스워드가 비어있습니다.", nameof(password));

        // 임시 고정 salt로 마스터 키 파생 (실제 암호화는 매번 랜덤 salt 사용)
        var tempSalt = "lssLib.Config.MasterSalt"u8.ToArray();
        _strKeyBytes = DeriveKey(password, tempSalt);
    }

    /// <summary>
    /// 원시 32바이트 키를 직접 설정합니다.
    /// </summary>
    /// <param name="key">32바이트 AES-256 키.</param>
    /// <exception cref="ArgumentException">키 길이가 32바이트가 아닌 경우.</exception>
    public static void SetKey(byte[] key)
    {
        if (key.Length != KeySize)
            throw new ArgumentException($"키 길이는 {KeySize}바이트여야 합니다.", nameof(key));
        _strKeyBytes = (byte[])key.Clone();
    }

    /// <summary>
    /// 암호화 키가 설정되어 있는지 확인합니다.
    /// </summary>
    public static bool HasKey => _strKeyBytes is not null;

    /// <summary>
    /// 키를 초기화합니다.
    /// </summary>
    public static void ClearKey()
    {
        if (_strKeyBytes is not null)
            Array.Clear(_strKeyBytes);
        _strKeyBytes = null;
    }

    #endregion

    #region §3 ─ 암호화 / 복호화

    /// <summary>
    /// 평문 문자열을 AES-256-GCM 으로 암호화하고 Base64 문자열로 반환합니다.
    /// </summary>
    /// <param name="plaintext">암호화할 평문.</param>
    /// <returns>Base64 인코딩된 암호문.</returns>
    /// <exception cref="InvalidOperationException">키가 설정되지 않은 경우.</exception>
    public static string Encrypt(string plaintext)
    {
        EnsureKey();
        ArgumentNullException.ThrowIfNull(plaintext);

        byte[] salt = RandomBytes(SaltSize);
        byte[] nonce = RandomBytes(NonceSize);
        byte[] key = DeriveKey(KeyBytesToBase64(), salt);  // 매 암호화마다 고유 키
        byte[] plain = Encoding.UTF8.GetBytes(plaintext);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        // 저장 포맷: salt(16) + nonce(12) + tag(16) + ciphertext
        byte[] blob = new byte[SaltSize + NonceSize + TagSize + cipher.Length];
        int pos = 0;
        salt.CopyTo(blob, pos); pos += SaltSize;
        nonce.CopyTo(blob, pos); pos += NonceSize;
        tag.CopyTo(blob, pos); pos += TagSize;
        cipher.CopyTo(blob, pos);

        // 보안: 파생 키 즉시 소거
        Array.Clear(key);

        return Convert.ToBase64String(blob);
    }

    /// <summary>
    /// Base64 암호문을 복호화하여 평문 문자열로 반환합니다.
    /// </summary>
    /// <param name="base64Ciphertext">암호화된 Base64 문자열.</param>
    /// <returns>복호화된 평문.</returns>
    /// <exception cref="InvalidOperationException">키가 설정되지 않은 경우.</exception>
    /// <exception cref="CryptographicException">복호화 실패 (키 불일치, 데이터 손상).</exception>
    public static string Decrypt(string base64Ciphertext)
    {
        EnsureKey();
        ArgumentNullException.ThrowIfNull(base64Ciphertext);

        byte[] blob = Convert.FromBase64String(base64Ciphertext);

        int minLen = SaltSize + NonceSize + TagSize;
        if (blob.Length < minLen)
            throw new CryptographicException("암호문이 너무 짧습니다.");

        int pos = 0;
        var salt = blob.AsSpan(pos, SaltSize); pos += SaltSize;
        var nonce = blob.AsSpan(pos, NonceSize); pos += NonceSize;
        var tag = blob.AsSpan(pos, TagSize); pos += TagSize;
        var cipher = blob.AsSpan(pos);

        byte[] key = DeriveKey(KeyBytesToBase64(), salt.ToArray());
        byte[] plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            Array.Clear(key);
        }
    }

    #endregion

    #region §4 ─ 저장 포맷 헬퍼

    /// <summary>
    /// 평문을 암호화하고 <c>ENC:</c> 접두사를 붙인 저장용 문자열로 반환합니다.
    /// </summary>
    public static string ToStoredValue(string plaintext) =>
        EncPrefix + Encrypt(plaintext);

    /// <summary>
    /// <c>ENC:</c> 접두사가 있으면 복호화하고, 없으면 원문을 그대로 반환합니다.
    /// </summary>
    /// <param name="storedValue">파일에서 읽은 원시 값.</param>
    /// <returns>복호화된 평문 또는 원문.</returns>
    public static string FromStoredValue(string storedValue)
    {
        if (storedValue.StartsWith(EncPrefix, StringComparison.Ordinal))
            return Decrypt(storedValue[EncPrefix.Length..]);
        return storedValue;
    }

    /// <summary>
    /// 저장된 값이 암호화된 값인지 확인합니다.
    /// </summary>
    public static bool IsEncryptedValue(string storedValue) =>
        storedValue.StartsWith(EncPrefix, StringComparison.Ordinal);

    #endregion

    #region §5 ─ 내부 헬퍼

    private static byte[] DeriveKey(string passwordOrBase64, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            passwordOrBase64,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

    private static string KeyBytesToBase64() =>
        Convert.ToBase64String(_strKeyBytes!);

    private static byte[] RandomBytes(int count)
    {
        byte[] buf = new byte[count];
        RandomNumberGenerator.Fill(buf);
        return buf;
    }

    private static void EnsureKey()
    {
        if (_strKeyBytes is null)
            throw new InvalidOperationException(
                "암호화 키가 설정되지 않았습니다. " +
                "ConfigEncryptor.SetPassword() 또는 SetKey() 를 먼저 호출하세요.");
    }

    #endregion
}