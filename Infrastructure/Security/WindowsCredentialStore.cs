using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Timer.Infrastructure.Security;

public sealed class WindowsCredentialStore
{
    private const int CredentialTypeGeneric = 1;
    private const int CredentialPersistLocalMachine = 2;

    public void Write(string target, string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        var credential = new CREDENTIAL
        {
            Type = CredentialTypeGeneric,
            Persist = CredentialPersistLocalMachine,
            TargetName = target,
            CredentialBlobSize = bytes.Length
        };

        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            credential.CredentialBlob = handle.AddrOfPinnedObject();
            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException("Failed to save credentials.");
            }
        }
        finally
        {
            handle.Free();
        }
    }

    public string? Read(string target)
    {
        if (!CredRead(target, CredentialTypeGeneric, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public void Delete(string target)
    {
        CredDelete(target, CredentialTypeGeneric, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree([In] IntPtr buffer);
}
