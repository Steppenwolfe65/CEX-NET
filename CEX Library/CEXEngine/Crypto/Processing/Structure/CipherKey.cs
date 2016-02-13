﻿#region Directives
using System;
using System.IO;
using System.Runtime.InteropServices;
using VTDev.Libraries.CEXEngine.Crypto.Common;
using VTDev.Libraries.CEXEngine.CryptoException;
#endregion

namespace VTDev.Libraries.CEXEngine.Crypto.Processing.Structure
{
    /// <summary>
    /// The CipherKey structure.
    /// <para>Used in conjunction with the <see cref="CipherStream"/> class. 
    /// This structure is used as the header for a single use key and vector set.</para>
    /// </summary>
    /// 
    /// <example>
    /// <description>Example of populating a CipherKey structure:</description>
    /// <code>
    /// CipherKey ck = new CipherKey(description);
    /// </code>
    /// </example>
    ///
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Processing.Factory.PackageFactory"/>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Common.CipherDescription"/>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Processing.CipherStream"/>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CipherKey
    {
        #region Constants
        private const int KEYID_SIZE = 16;
        private const int EXTKEY_SIZE = 16;
        private const int DESC_SIZE = 11;
        private const long KEYID_SEEK = 0;
        private const long EXTKEY_SEEK = KEYID_SIZE;
        private const long DESC_SEEK = KEYID_SIZE + EXTKEY_SIZE;
        #endregion

        #region Public Fields
        /// <summary>
        /// The unique 16 byte ID field used to identify this key. A null value auto generates this field
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = KEYID_SIZE)]
        public byte[] KeyID;
        /// <summary>
        /// An array of random bytes used to encrypt a message file extension. A null value auto generates this field
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = EXTKEY_SIZE)]
        public byte[] ExtensionKey;
        /// <summary>
        /// The <see cref="CipherDescription">CipherDescription</see> structure containing a complete description of the cipher instance
        /// </summary>
        [MarshalAs(UnmanagedType.Struct, SizeConst = DESC_SIZE)]
        public CipherDescription Description;
        #endregion

        #region Constructor
        /// <summary>
        /// CipherKey structure constructor.
        /// <para>KeyID and ExtRandom values must each be 16 bytes in length.
        /// If they are not specified they will be populated automatically.</para>
        /// </summary>
        /// 
        /// <param name="Description">The <see cref="CipherDescription">CipherDescription</see> structure containing a complete description of the cipher instance</param>
        /// <param name="KeyId">The unique 16 byte ID field used to identify this key. A null value auto generates this field</param>
        /// <param name="ExtensionKey">An array of random bytes used to encrypt a message file extension. A null value auto generates this field</param>
        /// 
        /// <exception cref="CryptoProcessingException">Thrown if either the KeyId or ExtensionKey fields are null or invalid</exception>
        public CipherKey(CipherDescription Description, byte[] KeyId = null, byte[] ExtensionKey = null)
        {
            this.Description = Description;

            if (KeyId == null)
            {
                this.KeyID = Guid.NewGuid().ToByteArray();
            }
            else if (KeyId.Length != KEYID_SIZE)
            {
                throw new CryptoProcessingException("CipherKey:CTor", "The KeyId must be exactly 16 bytes!", new ArgumentOutOfRangeException());
            }
            else
            {
                this.KeyID = KeyId;
            }

            if (ExtensionKey == null)
            {
                using (KeyGenerator gen = new KeyGenerator())
                    this.ExtensionKey = gen.GetBytes(16);
            }
            else if (ExtensionKey.Length != EXTKEY_SIZE)
            {
                throw new CryptoProcessingException("CipherKey:CTor", "The random extension field must be exactly 16 bytes!", new ArgumentOutOfRangeException());
            }
            else
            {
                this.ExtensionKey = ExtensionKey;
            }
        }

        /// <summary>
        /// Initialize the CipherKey structure using a Stream
        /// </summary>
        /// 
        /// <param name="KeyStream">The Stream containing the CipherKey</param>
        public CipherKey(Stream KeyStream)
        {
            BinaryReader reader = new BinaryReader(KeyStream);

            KeyID = reader.ReadBytes(KEYID_SIZE);
            ExtensionKey = reader.ReadBytes(EXTKEY_SIZE);
            Description = new CipherDescription(reader.ReadBytes(CipherDescription.GetHeaderSize()));
        }

        /// <summary>
        /// Initialize the CipherKey structure using a byte array
        /// </summary>
        /// 
        /// <param name="KeyArray">The byte array containing the CipherKey</param>
        public CipherKey(byte[] KeyArray) :
            this (new MemoryStream(KeyArray))
        {
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Reset all members of the CipherKey structure, including the CipherDescription
        /// </summary>
        public void Reset()
        {
            Description.Reset();

            if (KeyID != null)
                Array.Clear(KeyID, 0, KeyID.Length);
            if (ExtensionKey != null)
                Array.Clear(ExtensionKey, 0, ExtensionKey.Length);
        }

        /// <summary>
        /// Convert the CipherKey structure as a byte array
        /// </summary>
        /// 
        /// <returns>The byte array containing the CipherKey</returns>
        public byte[] ToBytes()
        {
            return ToStream().ToArray();
        }

        /// <summary>
        /// Convert the CipherKey structure to a MemoryStream
        /// </summary>
        /// 
        /// <returns>The MemoryStream containing the CipherKey</returns>
        public MemoryStream ToStream()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(KeyID);
            writer.Write(ExtensionKey);
            writer.Write(Description.ToBytes());
            stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }
        #endregion

        #region Getters
        /// <summary>
        /// Get the header Size in bytes
        /// </summary>
        /// 
        /// <returns>Header size</returns>
        public static int GetHeaderSize()
        {
            return KEYID_SIZE + EXTKEY_SIZE + DESC_SIZE;
        }

        /// <summary>
        /// Get the cipher description header
        /// </summary>
        /// 
        /// <param name="KeyStream">The stream containing a key package</param>
        /// 
        /// <returns>CipherDescription structure</returns>
        public static CipherDescription GetCipherDescription(Stream KeyStream)
        {
            KeyStream.Seek(DESC_SEEK, SeekOrigin.Begin);
            return new CipherDescription(KeyStream);
        }

        /// <summary>
        /// Get the extension key (16 bytes)
        /// </summary>
        /// 
        /// <param name="KeyStream">The stream containing the cipher key</param>
        /// 
        /// <returns>The file extension key</returns>
        public static byte[] GetExtensionKey(Stream KeyStream)
        {
            KeyStream.Seek(EXTKEY_SEEK, SeekOrigin.Begin);
            return new BinaryReader(KeyStream).ReadBytes(EXTKEY_SIZE);
        }

        /// <summary>
        /// Get the key id (16 bytes)
        /// </summary>
        /// 
        /// <param name="KeyStream">The stream containing a cipher key</param>
        /// 
        /// <returns>The file extension key</returns>
        public static byte[] GetKeyId(Stream KeyStream)
        {
            KeyStream.Seek(KEYID_SEEK, SeekOrigin.Begin);
            return new BinaryReader(KeyStream).ReadBytes(KEYID_SIZE);
        }
        #endregion

        #region Setters
        /// <summary>
        /// Set the CipherDescription structure
        /// </summary>
        /// 
        /// <param name="KeyStream">The stream containing a key package</param>
        /// <param name="Description">The CipherDescription structure</param>
        public static void SetCipherDescription(Stream KeyStream, CipherDescription Description)
        {
            KeyStream.Seek(DESC_SEEK, SeekOrigin.Begin);
            new BinaryWriter(KeyStream).Write(Description.ToBytes());
        }

        /// <summary>
        /// Set the ExtensionKey
        /// </summary>
        /// 
        /// <param name="KeyStream">The stream containing a cipher key</param>
        /// <param name="ExtensionKey">Array of 16 bytes containing the ExtensionKey</param>
        public static void SetExtensionKey(Stream KeyStream, byte[] ExtensionKey)
        {
            byte[] key = new byte[EXTKEY_SIZE];
            Array.Copy(ExtensionKey, 0, key, 0, ExtensionKey.Length < EXTKEY_SIZE ? ExtensionKey.Length : EXTKEY_SIZE);
            KeyStream.Seek(EXTKEY_SEEK, SeekOrigin.Begin);
            new BinaryWriter(KeyStream).Write(key);
        }

        /// <summary>
        /// Set the Key Id
        /// </summary>
        /// 
        /// <param name="KeyStream">The stream containing a cipher key</param>
        /// <param name="KeyId">Array of 16 bytes containing the key id</param>
        public static void SetKeyId(Stream KeyStream, byte[] KeyId)
        {
            byte[] id = new byte[KEYID_SIZE];
            Array.Copy(KeyId, 0, id, 0, KeyId.Length < KEYID_SIZE ? KeyId.Length : KEYID_SIZE);
            KeyStream.Seek(KEYID_SEEK, SeekOrigin.Begin);
            new BinaryWriter(KeyStream).Write(id);
        }
        #endregion

        #region Overrides
        /// <summary>
        /// Get the hash code for this object
        /// </summary>
        /// 
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            int hash = Description.GetHashCode();
            hash += KeyID.GetHashCode();
            hash += ExtensionKey.GetHashCode();

            return hash;
        }

        /// <summary>
        /// Compare this object instance with another
        /// </summary>
        /// 
        /// <param name="Obj">Object to compare</param>
        /// 
        /// <returns>True if equal, otherwise false</returns>
        public override bool Equals(Object Obj)
        {
            if (!(Obj is CipherKey))
                return false;

            CipherKey other = (CipherKey)Obj;

            if (Description.GetHashCode() != other.Description.GetHashCode())
                return false;
            if (!Compare.IsEqual(KeyID, other.KeyID))
                return false;
            if (!Compare.IsEqual(ExtensionKey, other.ExtensionKey))
                return false;

            return true;
        }

        /// <summary>
        /// Compare this object instance is equal to another
        /// </summary>
        /// 
        /// <param name="X">The first object</param>
        /// <param name="Y">The second object</param>
        /// 
        /// <returns>Equal</returns>
        public static bool operator ==(CipherKey X, CipherKey Y)
        {
            return X.Equals(Y);
        }

        /// <summary>
        /// Compare this object instance is not equal to another
        /// </summary>
        /// 
        /// <param name="X">The first object</param>
        /// <param name="Y">The second object</param>
        /// 
        /// <returns>Not equal</returns>
        public static bool operator !=(CipherKey X, CipherKey Y)
        {
            return !(X.Equals(Y));
        }
        #endregion
    }
}
