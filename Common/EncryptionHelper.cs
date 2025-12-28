using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DynamicDbApi.Common
{
    /// <summary>
    /// 加密助手类，提供AES、DES、RSA等加密解密方法
    /// </summary>
    public static class EncryptionHelper
    {
        #region AES加密解密
        /// <summary>
        /// AES加密字符串
        /// </summary>
        /// <param name="plainText">明文</param>
        /// <param name="key">密钥（长度：16、24或32字节）</param>
        /// <param name="iv">初始化向量（长度：16字节）</param>
        /// <returns>加密后的Base64字符串</returns>
        public static string AesEncrypt(string plainText, string key, string iv)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(key);
                aesAlg.IV = Encoding.UTF8.GetBytes(iv);

                var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }

                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        /// <summary>
        /// AES解密字符串
        /// </summary>
        /// <param name="cipherText">密文（Base64格式）</param>
        /// <param name="key">密钥（长度：16、24或32字节）</param>
        /// <param name="iv">初始化向量（长度：16字节）</param>
        /// <returns>解密后的明文</returns>
        public static string AesDecrypt(string cipherText, string key, string iv)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(key);
                aesAlg.IV = Encoding.UTF8.GetBytes(iv);

                var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// 生成AES密钥和IV
        /// </summary>
        /// <param name="keySize">密钥长度（128、192、256）</param>
        /// <returns>密钥和IV</returns>
        public static (string Key, string IV) GenerateAesKey(int keySize = 256)
        {
            using (var aesAlg = Aes.Create())
            {
                aesAlg.KeySize = keySize;
                aesAlg.GenerateKey();
                aesAlg.GenerateIV();
                
                return (
                    Convert.ToBase64String(aesAlg.Key),
                    Convert.ToBase64String(aesAlg.IV)
                );
            }
        }
        #endregion

        #region DES加密解密
        /// <summary>
        /// DES加密字符串
        /// </summary>
        /// <param name="plainText">明文</param>
        /// <param name="key">密钥（长度：8字节）</param>
        /// <param name="iv">初始化向量（长度：8字节）</param>
        /// <returns>加密后的Base64字符串</returns>
        public static string DesEncrypt(string plainText, string key, string iv)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using (var desAlg = DES.Create())
            {
                desAlg.Key = Encoding.UTF8.GetBytes(key);
                desAlg.IV = Encoding.UTF8.GetBytes(iv);

                var encryptor = desAlg.CreateEncryptor(desAlg.Key, desAlg.IV);

                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }

                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        /// <summary>
        /// DES解密字符串
        /// </summary>
        /// <param name="cipherText">密文（Base64格式）</param>
        /// <param name="key">密钥（长度：8字节）</param>
        /// <param name="iv">初始化向量（长度：8字节）</param>
        /// <returns>解密后的明文</returns>
        public static string DesDecrypt(string cipherText, string key, string iv)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            using (var desAlg = DES.Create())
            {
                desAlg.Key = Encoding.UTF8.GetBytes(key);
                desAlg.IV = Encoding.UTF8.GetBytes(iv);

                var decryptor = desAlg.CreateDecryptor(desAlg.Key, desAlg.IV);

                using (var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// 生成DES密钥和IV
        /// </summary>
        /// <returns>密钥和IV</returns>
        public static (string Key, string IV) GenerateDesKey()
        {
            using (var desAlg = DES.Create())
            {
                desAlg.GenerateKey();
                desAlg.GenerateIV();
                
                return (
                    Convert.ToBase64String(desAlg.Key),
                    Convert.ToBase64String(desAlg.IV)
                );
            }
        }
        #endregion

        #region RSA加密解密
        /// <summary>
        /// RSA加密字符串
        /// </summary>
        /// <param name="plainText">明文</param>
        /// <param name="publicKey">公钥</param>
        /// <returns>加密后的Base64字符串</returns>
        public static string RsaEncrypt(string plainText, string publicKey)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(publicKey);
                var encryptedData = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), RSAEncryptionPadding.OaepSHA256);
                return Convert.ToBase64String(encryptedData);
            }
        }

        /// <summary>
        /// RSA解密字符串
        /// </summary>
        /// <param name="cipherText">密文（Base64格式）</param>
        /// <param name="privateKey">私钥</param>
        /// <returns>解密后的明文</returns>
        public static string RsaDecrypt(string cipherText, string privateKey)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(privateKey);
                var decryptedData = rsa.Decrypt(Convert.FromBase64String(cipherText), RSAEncryptionPadding.OaepSHA256);
                return Encoding.UTF8.GetString(decryptedData);
            }
        }

        /// <summary>
        /// RSA签名
        /// </summary>
        /// <param name="data">待签名数据</param>
        /// <param name="privateKey">私钥</param>
        /// <returns>签名（Base64格式）</returns>
        public static string RsaSign(string data, string privateKey)
        {
            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(privateKey);
                var signature = rsa.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return Convert.ToBase64String(signature);
            }
        }

        /// <summary>
        /// RSA验证签名
        /// </summary>
        /// <param name="data">待验证数据</param>
        /// <param name="signature">签名（Base64格式）</param>
        /// <param name="publicKey">公钥</param>
        /// <returns>验证结果</returns>
        public static bool RsaVerify(string data, string signature, string publicKey)
        {
            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(publicKey);
                return rsa.VerifyData(Encoding.UTF8.GetBytes(data), Convert.FromBase64String(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        /// <summary>
        /// 生成RSA密钥对
        /// </summary>
        /// <param name="keySize">密钥长度（默认2048）</param>
        /// <returns>公钥和私钥</returns>
        public static (string PublicKey, string PrivateKey) GenerateRsaKeyPair(int keySize = 2048)
        {
            using (var rsa = RSA.Create())
            {
                rsa.KeySize = keySize;
                return (
                    rsa.ToXmlString(false), // 公钥
                    rsa.ToXmlString(true)   // 私钥
                );
            }
        }
        #endregion
    }
}