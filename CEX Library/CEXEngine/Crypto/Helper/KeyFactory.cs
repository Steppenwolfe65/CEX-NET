﻿#region Directives
using System;
using System.IO;
using VTDev.Libraries.CEXEngine.Crypto.Cipher.Symmetric.Block;
using VTDev.Libraries.CEXEngine.Crypto.Digest;
using VTDev.Libraries.CEXEngine.Crypto.Mode;
using VTDev.Libraries.CEXEngine.Crypto.Prng;
using VTDev.Libraries.CEXEngine.Crypto.Structures;
using VTDev.Libraries.CEXEngine.Utility;
#endregion

namespace VTDev.Libraries.CEXEngine.Crypto.Helper
{
    /// <summary>
    /// <h3>A helper class used to create or extract a Key Package file.</h3>
    /// <para>This class works in conjunction with the <see cref="KeyPackage"/> structure to create and manage key package files; encryption key bundles, that contain cipher Key and IV material, 
    /// and optionally an HMAC key used for message authentication.</para>
    /// </summary>
    /// 
    /// <example>
    /// <description>Example using the <see cref="Create(KeyPackage, Prngs, Digests)"/> method:</description>
    /// <code>
    /// // populate a KeyAuthority structure
    /// KeyAuthority authority =  new KeyAuthority(domainId, originId, packageId, packageTag, keyPolicy);
    /// // create a key file
    /// new KeyFactory(KeyPath, authority).Create(KeyPackage);
    /// </code>
    /// 
    /// <description>Example using the <see cref="Extract(byte[], out CipherDescription, out KeyParams, out byte[])"/> method to get an existing key for decryption:</description>
    /// <code>
    /// // populate a KeyAuthority structure
    /// KeyAuthority authority =  new KeyAuthority(domainId, originId, packageId, packageTag, keyPolicy);
    /// KeyParams keyparam;
    /// CipherDescription description;
    /// byte[] extKey;
    /// byte[] keyId;
    /// 
    /// // extract a key for decryption
    /// using (KeyFactory factory = new KeyFactory(KeyPath, authority))
    ///     factory.Extract(keyId, out description, out keyparam, out extKey);
    /// </code>
    /// 
    /// <description>Example using the <see cref="NextKey(out CipherDescription, out KeyParams, out byte[])"/> method to get an unused key for encryption:</description>
    /// <code>
    /// // populate a KeyAuthority structure
    /// KeyAuthority authority =  new KeyAuthority(domainId, originId, packageId, packageTag, keyPolicy);
    /// KeyParams keyparam;
    /// CipherDescription description;
    /// byte[] extKey;
    ///
    /// // get the next available encryption subkey
    /// using (KeyFactory factory = new KeyFactory(KeyPath, authority))
    ///     keyId = factory.NextKey(out description, out keyparam, out extKey)
    /// </code>
    /// </example>
    /// 
    /// <revisionHistory>
    /// <revision date="2015/01/23" version="1.3.0.0">Initial release</revision>
    /// <revision date="2015/09/23" version="1.3.2.0">Reconstructed and expanded to process CipherDescription, KeyAuthority, and KeyPackage structures</revision>
    /// </revisionHistory>
    /// 
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Structures.KeyPackage">VTDev.Libraries.CEXEngine.Crypto.Structures KeyPackage Structure</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Structures.KeyAuthority">VTDev.Libraries.CEXEngine.Crypto.Structures KeyAuthority structure</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Structures.CipherDescription">VTDev.Libraries.CEXEngine.Crypto.Structures CipherDescription Structure</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.KeyPolicies">VTDev.Libraries.CEXEngine.Crypto KeyPolicies Enumeration</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.KeyStates">VTDev.Libraries.CEXEngine.Crypto KeyStates Enumeration</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Prngs">VTDev.Libraries.CEXEngine.Crypto.Prngs Enumeration</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Digests">VTDev.Libraries.CEXEngine.Crypto.Digests Enumeration</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Helper.KeyGenerator">VTDev.Libraries.CEXEngine.Crypto.Helper.KeyGenerator class</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.KeyParams">VTDev.Libraries.CEXEngine.Crypto.KeyParams class</seealso>
    /// <seealso cref="VTDev.Libraries.CEXEngine.Crypto.Process.CipherStream">VTDev.Libraries.CEXEngine.Crypto.Process CipherStream class</seealso>
    /// 
    /// <remarks>
    /// <description><h4>Implementation Notes:</h4></description>
    /// <para>A KeyPackage file contains a <see cref="KeyAuthority"/> structure that defines its identity and security settings, 
    /// a <see cref="CipherDescription"/> that contains the settings required to create a specific cipher instance, and the 'subkey set', an array of unique subkey id strings and 
    /// policy flags that identify and control each subkey.</para>
    /// <para>A KeyPackage file can contain one subkey, or many thousands of subkeys. Each subkey provides a unique keying material, and can only be used once for encryption; 
    /// guaranteeing a unique Key, IV, and HMAC key is used for every single encryption cycle.</para>
    /// <para>Each subkey in the Key Package contains a unique policy flag, which can be used to mark a key as locked(decryption) or expired(encryption), or trigger an erasure 
    /// of a specific subkey after the key is read for decryption using the <see cref="Extract(byte[], out CipherDescription, out KeyParams, out byte[])"/> function.</para>
    /// 
    /// <list type="bullet">
    /// <item><description>Constructors may use a fully qualified path to a key file and the local <see cref="KeyAuthority"/>.</description></item>
    /// <item><description>The <see cref="Create(KeyPackage, Prngs, Digests)"/> method auto-generates the keying material.</description></item>
    /// <item><description>The Extract() method retrieves a populated cipher description (CipherDescription), key material (KeyParams), and the file extension key from the key file.</description></item>
    /// </list>
    /// </remarks>
    public sealed class KeyFactory : IDisposable
    {
        #region Constants
        // fewer than 10 subkeys per package is best security
        private const int SUBKEY_MAX = 100000;
        #endregion

        #region Fields
        private bool _isDisposed = false;
        private string _keyPath;
        private KeyPackage _keyPackage;
        private KeyAuthority _keyOwner;
        #endregion

        #region Properties
        /// <summary>
        /// The access rights available to the current user of this <see cref="KeyPackage"/>
        /// </summary>
        public KeyScope AccessScope { private set; get; }

        /// <summary>
        /// Are we the Creator of this KeyPackage
        /// </summary>
        public bool IsCreator { private set; get; }

        /// <summary>
        /// The KeyPackage <see cref="KeyPolicies">policy flags</see>
        /// </summary>
        public long KeyPolicy { private set; get; }

        /// <summary>
        /// The last error string
        /// </summary>
        public string LastError { private set; get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize this class with a key file path. 
        /// <para>If the key exixts, permissions are tested, otherwise this path is used as the new key path and file name.</para>
        /// </summary>
        /// 
        /// <param name="KeyPath">The fully qualified path to the key file to be read or created</param>
        /// <param name="Authority">The local KeyAuthority credentials structure</param>
        /// 
        /// <exception cref="System.FormatException">Thrown if an empty KeyPath is used</exception>
        public KeyFactory(string KeyPath, KeyAuthority Authority)
        {
            if (string.IsNullOrEmpty(KeyPath))
                throw new FormatException("The key file path is empty!");

            // store authority
            _keyOwner = Authority;
            // file path or destination
            _keyPath = KeyPath;

            if (File.Exists(_keyPath))
                AccessScope = Authenticate();
        }

        /// <summary>
        /// Finalizer: ensure resources are destroyed
        /// </summary>
        ~KeyFactory()
        {
            Dispose(false);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Authentication tests; specific target domain or identity, passphrase, 
        /// and export permissions within the KeyPackage key policy settings are checked
        /// </summary>
        /// 
        /// <returns>Authorized to use this key</returns>
        public KeyScope Authenticate()
        {
            try
            {
                // get the key headers
                _keyPackage = GetPackage();
                // store the master policy flag
                KeyPolicy = _keyPackage.KeyPolicy;
                // did we create this key
                IsCreator = Compare.AreEqual(_keyOwner.OriginId, _keyPackage.Authority.OriginId);

                // key made by master auth, valid only if authenticated by PackageAuth, IdentityRestrict or DomainRestrict
                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.MasterAuth))
                {
                    if (Compare.AreEqual(_keyOwner.DomainId, _keyPackage.Authority.DomainId))
                    {
                        LastError = "";
                        return KeyScope.Creator;
                    }
                    else if (Compare.AreEqual(_keyOwner.PackageId, _keyPackage.Authority.PackageId))
                    {
                        LastError = "";
                        return KeyScope.Creator;
                    }
                    else if (Compare.AreEqual(_keyOwner.TargetId, _keyPackage.Authority.TargetId))
                    {
                        LastError = "";
                        return KeyScope.Creator;
                    }
                }

                // the key targets a specific installation identity
                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.IdentityRestrict))
                {
                    // test only if not creator
                    if (!Compare.AreEqual(_keyOwner.OriginId, _keyPackage.Authority.OriginId))
                    {
                        // owner target field is set as a target OriginId hash
                        if (!Compare.AreEqual(_keyOwner.TargetId, _keyPackage.Authority.TargetId))
                        {
                            LastError = "You are not the intendant recipient of this key! Access is denied.";
                            return KeyScope.NoAccess;
                        }
                    }
                }

                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.DomainRestrict))
                {
                    // the key is domain restricted
                    if (!Compare.AreEqual(_keyOwner.DomainId, _keyPackage.Authority.DomainId))
                    {
                        LastError = "Domain identification check has failed! You must be a member of the same Domain as the Creator of this key.";
                        return KeyScope.NoAccess;
                    }
                }

                // the key package id is an authentication passphrase hash
                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.PackageAuth))
                {
                    if (!Compare.AreEqual(_keyOwner.PackageId, _keyPackage.Authority.PackageId))
                    {
                        LastError = "Key Package authentication has failed! Access is denied.";
                        return KeyScope.NoAccess;
                    }
                }

                // test for volatile flag
                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.Volatile))
                {
                    if (_keyPackage.Authority.OptionFlag != 0 && _keyPackage.Authority.OptionFlag < DateTime.Now.Ticks)
                    {
                        LastError = "This key has expired and can no longer be used! Access is denied.";
                        return KeyScope.NoAccess;
                    }
                }

                // only the key creator is allowed access 
                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.NoExport))
                {
                    if (!Compare.AreEqual(_keyOwner.OriginId, _keyPackage.Authority.OriginId))
                    {
                        LastError = "Only the Creator of this key is authorized! Access is denied.";
                        return KeyScope.NoAccess;
                    }
                }

                LastError = "";
                return IsCreator ? KeyScope.Creator : KeyScope.Operator;
            }
            catch (Exception Ex)
            {
                LastError = Ex.Message;
                return KeyScope.NoAccess;
            }
        }

        /// <summary>
        /// Test a key to see if it contains a subkey with a specific id
        /// </summary>
        /// 
        /// <param name="KeyId">The subkey id to test</param>
        /// 
        /// <returns>The index of the subkey, or -1 if key is not in the KeyPackage</returns>
        /// 
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the user has insufficient access rights to access this KeyPackage</exception>
        public int ContainsSubKey(byte[] KeyId)
        {
            if (AccessScope.Equals(KeyScope.NoAccess))
                throw new UnauthorizedAccessException("You do not have permission to access this key!");

            for (int i = 0; i < _keyPackage.SubKeyID.Length; i++)
            {
                if (Utility.Compare.AreEqual(KeyId, _keyPackage.SubKeyID[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Create a key file using a <see cref="KeyPackage"/> structure; containing the cipher description and operating ids and flags.
        /// </summary>
        /// 
        /// <param name="Package">The <see cref="KeyPackage">Key Header</see> containing the cipher description and operating ids and flags</param>
        /// <param name="SeedEngine">The <see cref="Prngs">Random Generator</see> used to create the stage I seed material during key generation.</param>
        /// <param name="DigestEngine">The <see cref="Digests">Digest Engine</see> used in the stage II phase of key generation.</param>
        /// 
        /// <exception cref="System.IO.FileLoadException">A key file exists at the path specified</exception>
        /// <exception cref="System.UnauthorizedAccessException">The key file path is read only</exception>
        /// <exception cref="System.FormatException">Thrown if the CipherDescription of KeyAuthority structures are invalid</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">The number of SubKeys specified is either less than 1 or more than the maximum allowed (100,000)</exception>
        public void Create(KeyPackage Package, Prngs SeedEngine = Prngs.CSPRng, Digests DigestEngine = Digests.SHA512)
        {
            // if you are getting exceptions.. read the docs!
            if (File.Exists(_keyPath))
                throw new FileLoadException("The key file exists! Can not overwrite an existing key file, choose a different path.");
            if (!Utility.DirectoryUtilities.DirectoryIsWritable(Path.GetDirectoryName(_keyPath)))
                throw new UnauthorizedAccessException("The selected directory is read only! Choose a different path.");
            if (!CipherDescription.IsValid(Package.Description))
                throw new FormatException("The key package cipher settings are invalid!");
            if (!KeyAuthority.IsValid(Package.Authority))
                throw new FormatException("The key package key authority settings are invalid!");
            if (Package.SubKeyCount < 1)
                throw new ArgumentOutOfRangeException("The key package must contain at least 1 key!");
            if (Package.SubKeyCount > SUBKEY_MAX)
                throw new ArgumentOutOfRangeException(String.Format("The key package can not contain more than {0} keys!", SUBKEY_MAX));

            // get the size of a subkey set
            int subKeySize = Package.Description.KeySize;

            if (Package.Description.IvSize > 0)
                subKeySize += Package.Description.IvSize;
            
            if (Package.Description.MacSize > 0)
                subKeySize += Package.Description.MacSize;

            if (subKeySize < 0)
                throw new Exception("The key package cipher settings are invalid!");

            try
            {
                // store the auth struct and policy
                _keyOwner = Package.Authority;
                this.KeyPolicy = Package.KeyPolicy;
                // get the serialized header
                byte[] header = ((MemoryStream)KeyPackage.Serialize(Package)).ToArray();
                // size key buffer
                byte[] buffer = new byte[subKeySize * Package.SubKeyCount];

                // generate the keying material
                using (KeyGenerator keyGen = new KeyGenerator(SeedEngine, DigestEngine))
                    keyGen.GetBytes(buffer);

                using (BinaryWriter keyWriter = new BinaryWriter(new FileStream(_keyPath, FileMode.Create, FileAccess.Write)))
                {
                    // pre-set the size to avoid fragmentation
                    keyWriter.BaseStream.SetLength(KeyPackage.GetHeaderSize(Package) + (subKeySize * Package.SubKeyCount));

                    if (IsEncrypted(Package.KeyPolicy))
                    {
                        // add policy flags, only part of key not encrypted
                        keyWriter.Write(Package.KeyPolicy);
                        // get salt, return depends on auth flag settings
                        byte[] salt = GetSalt();
                        // create a buffer for encrypted data
                        int hdrLen = header.Length - KeyPackage.GetPolicyOffset();
                        byte[] data = new byte[buffer.Length + hdrLen];
                        // copy header and key material
                        Buffer.BlockCopy(header, KeyPackage.GetPolicyOffset(), data, 0, hdrLen);
                        Buffer.BlockCopy(buffer, 0, data, hdrLen, buffer.Length);
                        // encrypt the key and header
                        TransformBuffer(data, salt);
                        // write to file
                        keyWriter.Write(data);
                        // don't wait for gc
                        Array.Clear(salt, 0, salt.Length);
                        Array.Clear(data, 0, data.Length);
                    }
                    else
                    {
                        // write the keypackage header
                        keyWriter.Write(header, 0, header.Length);
                        // write the keying material
                        keyWriter.Write(buffer, 0, buffer.Length);
                    }
                }
                // cleanup
                Array.Clear(header, 0, header.Length);
                Array.Clear(buffer, 0, buffer.Length);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Extract a subkey set (KeyParam), a file extension key, and a CipherDescription. 
        /// <para>Used only when calling a Decryption function to get a specific subkey 
        /// The KeyId field corresponds with the KeyId field contained in a MessageHeader structure.</para>
        /// </summary>
        /// 
        /// <param name="KeyId">The KeyId array used to identify a subkey set; set as the KeyId in a MessageHeader structure</param>
        /// <param name="Description">out: The CipherDescription structure; the properties required to create a specific cipher instance</param>
        /// <param name="KeyParam">out: The KeyParams class containing a unique key, initialization vector and HMAC key</param>
        /// <param name="ExtensionKey">out: The random key used to encrypt the message file extension</param>
        /// 
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the user has insufficient access rights to access this KeyPackage</exception>
        /// <exception cref="System.ArgumentException">The KeyPackage does not contain the KeyId specified. Use the <see cref="ContainsSubKey(byte[])"/> to test for key existence,</exception>
        public void Extract(byte[] KeyId, out CipherDescription Description, out KeyParams KeyParam, out byte[] ExtensionKey)
        {
            if (AccessScope.Equals(KeyScope.NoAccess))
                throw new UnauthorizedAccessException("You do not have permission to access this key!");

            try
            {
                long keyPos;
                int index;
                // get the key data
                MemoryStream keyStream = GetKeyStream();

                // get the keying materials starting offset within the key file
                keyPos = KeyPackage.SubKeyOffset(keyStream, KeyId);

                if (keyPos == -1)
                    throw new ArgumentException("This package does not contain the key file!");

                // get the index
                index = KeyPackage.SubKeyFind(keyStream, KeyId);

                // key flagged SingleUse was used for decryption and is locked out
                if (KeyPackage.KeyHasPolicy(_keyPackage.SubKeyPolicy[index], (long)KeyStates.Locked))
                    throw new Exception("SubKey is locked. The subkey has a single use policy and was previously used to decrypt the file.");
                // key flagged PostOverwrite was used for decryption and was erased
                if (KeyPackage.KeyHasPolicy(_keyPackage.SubKeyPolicy[index], (long)KeyStates.Erased))
                    throw new Exception("SubKey is erased. The subkey has a post erase policy and was previously used to decrypt the file.");

                // get the cipher description
                Description = _keyPackage.Description;
                // get the keying material
                KeyParam = GetKeySet(keyStream, _keyPackage.Description, keyPos);
                // encrypts the file extension
                ExtensionKey = _keyPackage.ExtensionKey;

                // test flags for overwrite or single use policies
                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.PostOverwrite))
                    KeyPackage.SubKeySetPolicy(keyStream, index, (long)KeyStates.Erased);
                else if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.SingleUse))
                    KeyPackage.SubKeySetPolicy(keyStream, index, (long)KeyStates.Locked);

                // post overwrite flag set, erase the subkey
                if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.PostOverwrite))
                {
                    int keySize = Description.KeySize + Description.IvSize + Description.MacSize;
                    // overwrite the region within file
                    Erase(keyPos, keySize);
                    // clear this section of the key
                    keyStream.Seek(keyPos, SeekOrigin.Begin);
                    keyStream.Write(new byte[keySize], 0, keySize);
                }

                // write to file
                WriteKeyStream(keyStream);
            }
            catch 
            {
                throw;
            }
        }

        /// <summary>
        /// Test the KeyPackage for remaining valid subkeys
        /// </summary>
        /// 
        /// <returns>KeyPackage contains subkeys that are valid for encryption</returns>
        /// 
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the user has insufficient access rights to access this KeyPackage</exception>
        public bool HasExpired()
        {
            if (AccessScope.Equals(KeyScope.NoAccess))
                throw new UnauthorizedAccessException("You do not have permission to access this key!");

            for (int i = 0; i < _keyPackage.SubKeyCount; i++)
            {
                if (!KeyPackage.KeyHasPolicy(_keyPackage.SubKeyPolicy[i], (long)KeyStates.Expired))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Test a KeyPackage subkey for expired status
        /// </summary>
        /// 
        /// <param name="KeyId">The subkey id to test</param>
        /// 
        /// <returns>Returns true if subkey has expired and can not be used for encryption, false if a valid key</returns>
        /// 
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the user has insufficient access rights to access this KeyPackage</exception>
        public bool HasExpired(byte[] KeyId)
        {
            if (AccessScope.Equals(KeyScope.NoAccess))
                throw new UnauthorizedAccessException("You do not have permission to access this key!");

            int index = ContainsSubKey(KeyId);
            if (index < 0)
                return true;

            return (KeyPackage.KeyHasPolicy(_keyPackage.SubKeyPolicy[index], (int)KeyStates.Expired));
        }

        /// <summary>
        /// Get information about the key file in the form of an <see cref="PackageInfo"/> structure
        /// </summary>
        /// 
        /// <returns>A <see cref="PackageInfo"/> structure</returns>
        /// 
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the user has insufficient access rights to access this KeyPackage</exception>
        public PackageInfo KeyInfo()
        {
            if (AccessScope.Equals(KeyScope.NoAccess))
                throw new UnauthorizedAccessException("You do not have permission to access this key!");

            PackageInfo info = new PackageInfo(_keyPackage);

            // return limited data
            if (KeyPackage.KeyHasPolicy(KeyPolicy, (long)KeyPolicies.NoNarrative))
            {
                info.Origin = Guid.Empty;
                info.Policies.Clear();
            }

            return info;
        }

        /// <summary>
        /// Extract the next valid subkey set (Expired flag not set) as a KeyParam, and a CipherDescription structure. 
        /// <para>Used only when calling a Encryption function.</para>
        /// </summary>
        /// 
        /// <param name="Description">out: The CipherDescription structure; the properties required to create a specific cipher instance</param>
        /// <param name="KeyParam">out: The KeyParams class containing a unique key, initialization vector and HMAC key</param>
        /// <param name="ExtensionKey">out: The random key used to encrypt the message file extension</param>
        /// 
        /// <returns>The KeyId array used to identify a subkey set; set as the KeyId in a MessageHeader structure</returns>
        /// 
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the user has insufficient access rights to perform encryption with this key.</exception>
        public byte[] NextKey(out CipherDescription Description, out KeyParams KeyParam, out byte[] ExtensionKey)
        {
            if (!AccessScope.Equals(KeyScope.Creator))
                throw new UnauthorizedAccessException("You do not have permission to encrypt with this key!");

            try
            {
                // get the key data
                MemoryStream keyStream = GetKeyStream();
                // get the next unused key for encryption
                int index = KeyPackage.SubKeyNextValid(keyStream);

                if (index == -1)
                    throw new Exception("The key file has expired! There are no keys left available for encryption.");

                // get the cipher description
                Description = _keyPackage.Description;
                // get the file extension key
                ExtensionKey = _keyPackage.ExtensionKey;
                // store the subkey identity, this is written into the message header to identify the subkey
                byte[] keyId = _keyPackage.SubKeyID[index];
                // get the starting position of the keying material within the package
                long keyPos = KeyPackage.SubKeyOffset(keyStream, keyId);

                // no unused keys in the package file
                if (keyPos == -1)
                    throw new Exception("The key file has expired! There are no keys left available for encryption.");

                // get the keying material
                KeyParam = GetKeySet(keyStream, _keyPackage.Description, keyPos);
                // mark the subkey as expired
                KeyPackage.SubKeySetPolicy(keyStream, index, (long)KeyStates.Expired);
                // write to file
                WriteKeyStream(keyStream);
                // return the subkey id
                return keyId;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get the policy flags for a subkey
        /// </summary>
        /// 
        /// <param name="KeyId">Id of the subkey to query</param>
        /// 
        /// <returns>Sub key policy flag, or -1 if not key id found</returns>
        /// 
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the user has insufficient access rights to access this KeyPackage</exception>
        public Int64 Policy(byte[] KeyId)
        {
            if (AccessScope.Equals(KeyScope.NoAccess))
                throw new UnauthorizedAccessException("You do not have permission to access this key!");

            int index = ContainsSubKey(KeyId);
            if (index < 0)
                return -1;

            if (index > _keyPackage.SubKeyPolicy.Length)
                return -1;

            return _keyPackage.SubKeyPolicy[index];
        }
        #endregion

        #region Private Methods
        /// <remarks>
        /// 4 stage overwrite: random, reverse random, ones, zeros. 
        /// Last overwrite stage is zeros in Extract() method.
        /// </remarks>
        private void Erase(long Offset, long Length)
        {
            byte[] buffer =  new byte[Length];

            // get p-rand buffer
            using (CSPRng csp = new CSPRng())
                csp.GetBytes(buffer);

            // rand
            Overwrite(buffer, Offset, Length);
            // reverse rand
            Array.Reverse(buffer);
            Overwrite(buffer, Offset, Length);
            // ones
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)255;
            Overwrite(buffer, Offset, Length);
        }

        /// <remarks>
        /// Returns the KeyPackage structure
        /// </remarks>
        private KeyPackage GetPackage()
        {
            MemoryStream keyStream = GetKeyStream();
            KeyPackage package = KeyPackage.DeSerialize(keyStream);
            keyStream.Dispose();

            return package;
        }

        /// <remarks>
        /// Returns the populated KeyParams class
        /// </remarks>
        private KeyParams GetKeySet(MemoryStream KeyStream, CipherDescription Description, long Position)
        {
            KeyParams keyParam;
            KeyStream.Seek(Position, SeekOrigin.Begin);

            // create the keyparams class
            if (Description.MacSize > 0 && Description.IvSize > 0)
            {
                byte[] key = new byte[Description.KeySize];
                byte[] iv = new byte[Description.IvSize];
                byte[] ikm = new byte[Description.MacSize];

                KeyStream.Read(key, 0, key.Length);
                KeyStream.Read(iv, 0, iv.Length);
                KeyStream.Read(ikm, 0, ikm.Length);
                keyParam = new KeyParams(key, iv, ikm);
            }
            else if (Description.IvSize > 0)
            {
                byte[] key = new byte[Description.KeySize];
                byte[] iv = new byte[Description.IvSize];

                KeyStream.Read(key, 0, key.Length);
                KeyStream.Read(iv, 0, iv.Length);
                keyParam = new KeyParams(key, iv);
            }
            else if (Description.MacSize > 0)
            {
                byte[] key = new byte[Description.KeySize];
                byte[] ikm = new byte[Description.MacSize];

                KeyStream.Read(key, 0, key.Length);
                KeyStream.Read(ikm, 0, ikm.Length);
                keyParam = new KeyParams(key, null, ikm);
            }
            else
            {
                byte[] key = new byte[Description.KeySize];
                KeyStream.Read(key, 0, key.Length);
                keyParam = new KeyParams(key);
            }

            return keyParam;
        }

        /// <remarks>
        /// Get the working copy of the key package as a stream
        /// </remarks>
        private MemoryStream GetKeyStream()
        {
            MemoryStream keyStream = null;

            try
            {
                using (BinaryReader keyReader = new BinaryReader(new FileStream(_keyPath, FileMode.Open, FileAccess.Read, FileShare.None)))
                {
                    // output stream and writer
                    keyStream = new MemoryStream((int)keyReader.BaseStream.Length);
                    BinaryWriter keyWriter = new BinaryWriter(keyStream);

                    // add policy flags
                    this.KeyPolicy = keyReader.ReadInt64();
                    keyWriter.Write(KeyPolicy);
                    // get the data
                    byte[] data = keyReader.ReadBytes((int)(keyReader.BaseStream.Length - KeyPackage.GetPolicyOffset()));

                    // decrypt
                    if (IsEncrypted(KeyPolicy))
                    {
                        // get the salt
                        byte[] salt = GetSalt();
                        // decrypt the key
                        TransformBuffer(data, salt);
                        // clear the salt
                        Array.Clear(salt, 0, salt.Length);
                    }

                    // copy to stream
                    keyWriter.Write(data);
                    // don't wait for gc
                    Array.Clear(data, 0, data.Length);
                    // reset position
                    keyStream.Seek(0, SeekOrigin.Begin);
                }
                return keyStream;
            }
            catch
            {
                throw;
            }
        }

        /// <remarks>
        /// Get the salt value used to encrypt the key.
        /// Salt is derived from authentication fields in the package header.
        /// </remarks>
        private byte[] GetSalt()
        {
            byte[] salt = null;
            int offset = 0;

            if (HasFlag(KeyPolicy, KeyPolicies.PackageAuth))
            {
                // hash of user passphrase
                salt = new byte[_keyOwner.PackageId.Length];
                Buffer.BlockCopy(_keyOwner.PackageId, 0, salt, 0, salt.Length);
                offset += _keyOwner.PackageId.Length;
            }

            if (HasFlag(KeyPolicy, KeyPolicies.DomainRestrict))
            {
                // hashed domain name or group secret
                if (salt == null)
                    salt = new byte[_keyOwner.DomainId.Length];
                else
                    Array.Resize<byte>(ref salt, offset + _keyOwner.DomainId.Length);

                Buffer.BlockCopy(_keyOwner.DomainId, 0, salt, offset, _keyOwner.DomainId.Length);
                offset += _keyOwner.DomainId.Length;
            }

            if (HasFlag(KeyPolicy, KeyPolicies.IdentityRestrict))
            {
                // add the target id
                if (salt == null)
                    salt = new byte[_keyOwner.TargetId.Length];
                else
                    Array.Resize<byte>(ref salt, offset + _keyOwner.TargetId.Length);

                Buffer.BlockCopy(_keyOwner.DomainId, 0, salt, offset, _keyOwner.TargetId.Length);
            }

            return salt;
        }

        private bool HasFlag(long Flags, KeyPolicies Policy)
        {
            return ((Flags & (long)Policy) == (long)Policy);
        }

        private bool IsEncrypted(long Policies)
        {
            return HasFlag(Policies, KeyPolicies.PackageAuth);
        }

        private bool IsEncrypted(KeyPackage Package)
        {
            return HasFlag(Package.KeyPolicy, KeyPolicies.PackageAuth);
        }

        /// <remarks>
        /// Overwrite a section of the key file, used by the PostOverwrite policy
        /// </remarks>
        private void Overwrite(byte[] KeyData, long Offset, long Length)
        {
            using (FileStream outputWriter = new FileStream(_keyPath, FileMode.Open, FileAccess.Write, FileShare.None, KeyData.Length, FileOptions.WriteThrough))
            {
                outputWriter.Seek(Offset, SeekOrigin.Begin);
                outputWriter.Write(KeyData, 0, KeyData.Length);
            }
        }

        /// <remarks>
        /// Encrypts the key package buffer
        /// </remarks>
        private void TransformBuffer(byte[] KeyData, byte[] Salt)
        {
            byte[] kvm = new byte[48];

            // use salt to derive key and counter vector
            using (Keccak512 digest = new Keccak512(384))
                kvm = digest.ComputeHash(Salt);

            byte[] key = new byte[32];
            byte[] iv = new byte[16];
            Buffer.BlockCopy(kvm, 0, key, 0, key.Length);
            Buffer.BlockCopy(kvm, key.Length, iv, 0, iv.Length);

            using (KeyParams keyparam = new KeyParams(key, iv))
            {
                // 40 rounds of serpent
                using (CTR cipher = new CTR(new SPX(40)))
                {
                    cipher.Initialize(true, keyparam);
                    cipher.Transform(KeyData, KeyData);
                }
            }
        }

        /// <remarks>
        /// Writes a memorystream to the key package file
        /// </remarks>
        private void WriteKeyStream(MemoryStream KeyStream)
        {
            KeyStream.Seek(0, SeekOrigin.Begin);

            try
            {
                using (BinaryWriter keyWriter = new BinaryWriter(new FileStream(_keyPath, FileMode.Open, FileAccess.Write, FileShare.None)))
                {
                    using (BinaryReader keyReader = new BinaryReader(KeyStream))
                    {
                        // policy flag is not encrypted
                        long policies = keyReader.ReadInt64();
                        keyWriter.Write(policies);
                        // get the header and keying material
                        byte[] data = new byte[KeyStream.Length - KeyPackage.GetPolicyOffset()];
                        KeyStream.Read(data, 0, data.Length);

                        if (IsEncrypted(policies))
                        {
                            // get the salt
                            byte[] salt = GetSalt();
                            // decrypt the key and header
                            TransformBuffer(data, salt);
                            Array.Clear(salt, 0, salt.Length);
                        }

                        // copy to file
                        keyWriter.Write(data, 0, data.Length);
                        // clean up
                        Array.Clear(data, 0, data.Length);
                    }
                }

                KeyStream.Dispose();
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region IDispose
        /// <summary>
        /// Dispose of this class
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool Disposing)
        {
            if (!_isDisposed && Disposing)
            {
                try
                {
                    _keyPackage.Reset();

                    if (_keyPath != null)
                        _keyPath = null;
                }
                finally
                {
                    _isDisposed = true;
                }
            }
        }
        #endregion
    }
}