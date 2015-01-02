﻿#region Directives
using System;
#endregion

namespace VTDev.Libraries.CEXEngine.Crypto.Macs
{
    /// <summary>
    /// Message Authentication Code (MAC) Interface
    /// </summary>
    public interface IMac : IDisposable
    {
        /// <summary>
        /// Get: The Digests internal blocksize in bytes
        /// </summary>
        int BlockSize { get; }

        /// <summary>
        /// Get: Size of returned digest in bytes
        /// </summary>
        int DigestSize { get; }

        /// <summary>
        /// Get: Algorithm name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Update the buffer
        /// </summary>
        /// 
        /// <param name="Input">Input data [bytes]</param>
        /// <param name="InOffset">Offset within Input array</param>
        /// <param name="Length">Amount of data to process in bytes</param>
        void BlockUpdate(byte[] Input, int InOffset, int Length);

        /// <summary>
        /// Get the Mac hash value
        /// </summary>
        /// 
        /// <param name="Input">Input data [bytes]</param>
        /// 
        /// <returns>Mac Hash value</returns>
        byte[] ComputeMac(byte[] Input);

        /// <summary>
        /// Process the last block of data
        /// </summary>
        /// 
        /// <param name="Output">The hash value return</param>
        /// <param name="Offset">The offset in the data</param>
        /// 
        /// <returns>bytes processed</returns>
        int DoFinal(byte[] Output, int Offset);

        /// <summary>
        /// Initialize the HMAC
        /// </summary>
        /// 
        /// <param name="Key">HMAC key</param>
        void Init(byte[] Key);

        /// <summary>
        /// Reset the internal state
        /// </summary>
        void Reset();

        /// <summary>
        /// Update the digest with a single byte
        /// </summary>
        /// 
        /// <param name="Input">Input byte</param>
        void Update(byte Input);
    }
}
