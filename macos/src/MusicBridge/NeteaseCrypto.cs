using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace MusicBridge;

internal static class NeteaseCrypto
{
	private const string PresetKey = "0CoJUm6Qyw8W8jud";

	private const string Iv = "0102030405060708";

	private const string PublicExponentHex = "010001";

	private const string ModulusHex = "00e0b509f6259df8642dbc35662901477df22677ec152b5ff68ace615bb7b725152b3ab17a876aea8a5aa76d2e417629ec4ee341f56135fccf695280104e0312ecbda92557c93870114af6c9d05c4f7f0c3685b7a46bee255932575cce10b424d813cfe4875d3e82047b97ddef52741d546b8e289dc6935b3ece0462db0a22b8e7";

	private const string KeyCharset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	public static void Encrypt(string json, out string paramsValue, out string encSecKey)
	{
		string text = RandomKey(16);
		string plain = AesCbcBase64(json, "0CoJUm6Qyw8W8jud");
		paramsValue = AesCbcBase64(plain, text);
		encSecKey = RsaNoPadding(text);
	}

	private static string RandomKey(int length)
	{
		byte[] array = new byte[length];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		StringBuilder stringBuilder = new StringBuilder(length);
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			stringBuilder.Append("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[b % "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".Length]);
		}
		return stringBuilder.ToString();
	}

	private static string AesCbcBase64(string plain, string key)
	{
		using Aes aes = Aes.Create();
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;
		aes.KeySize = 128;
		aes.Key = Encoding.UTF8.GetBytes(key);
		aes.IV = Encoding.UTF8.GetBytes("0102030405060708");
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		byte[] bytes = Encoding.UTF8.GetBytes(plain);
		return Convert.ToBase64String(cryptoTransform.TransformFinalBlock(bytes, 0, bytes.Length));
	}

	private static string RsaNoPadding(string secretKey)
	{
		char[] array = secretKey.ToCharArray();
		Array.Reverse(array);
		BigInteger value = BigEndianToBigInteger(Encoding.UTF8.GetBytes(new string(array)));
		BigInteger exponent = BigEndianToBigInteger(HexToBytes("010001"));
		BigInteger modulus = BigEndianToBigInteger(HexToBytes("00e0b509f6259df8642dbc35662901477df22677ec152b5ff68ace615bb7b725152b3ab17a876aea8a5aa76d2e417629ec4ee341f56135fccf695280104e0312ecbda92557c93870114af6c9d05c4f7f0c3685b7a46bee255932575cce10b424d813cfe4875d3e82047b97ddef52741d546b8e289dc6935b3ece0462db0a22b8e7"));
		return BigIntegerToHex(BigInteger.ModPow(value, exponent, modulus), 256);
	}

	private static BigInteger BigEndianToBigInteger(byte[] bigEndian)
	{
		byte[] array = new byte[bigEndian.Length + 1];
		for (int i = 0; i < bigEndian.Length; i++)
		{
			array[i] = bigEndian[bigEndian.Length - 1 - i];
		}
		array[bigEndian.Length] = 0;
		return new BigInteger(array);
	}

	private static string BigIntegerToHex(BigInteger value, int hexLength)
	{
		byte[] array = value.ToByteArray();
		StringBuilder stringBuilder = new StringBuilder(hexLength);
		int num = array.Length - 1;
		while (num > 0 && array[num] == 0)
		{
			num--;
		}
		for (int num2 = num; num2 >= 0; num2--)
		{
			stringBuilder.Append(array[num2].ToString("x2"));
		}
		string text = stringBuilder.ToString();
		if (text.Length < hexLength)
		{
			return text.PadLeft(hexLength, '0');
		}
		return text;
	}

	private static byte[] HexToBytes(string hex)
	{
		if (hex.Length % 2 != 0)
		{
			hex = "0" + hex;
		}
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
