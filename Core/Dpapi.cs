using System.Runtime.InteropServices;

namespace TzIsland;

/// <summary>
/// DPAPI (CurrentUser) sem dependência extra: os fixados da área de transferência ficam em disco
/// (texto e imagens) e eram gravados em claro. Numa app que já reconhece conteúdo privado de
/// gerenciador de senhas, o mínimo é que o que sai para o disco esteja protegido pelo usuário logado.
/// P/Invoke direto em crypt32.dll em vez do pacote System.Security.Cryptography.ProtectedData.
/// </summary>
public static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr h);

    const int UIForbidden = 0x1;

    public static byte[] Protect(byte[] plain)
    {
        if (plain.Length == 0) return Array.Empty<byte>();
        var input = new DataBlob { Size = plain.Length, Data = Marshal.AllocHGlobal(plain.Length) };
        try
        {
            Marshal.Copy(plain, 0, input.Data, plain.Length);
            if (!CryptProtectData(ref input, "TzIsland", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UIForbidden, out var output))
                throw new InvalidOperationException("CryptProtectData falhou: " + Marshal.GetLastWin32Error());
            try { return Copy(output); }
            finally { if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }

    public static byte[] Unprotect(byte[] cipher)
    {
        if (cipher.Length == 0) return Array.Empty<byte>();
        var input = new DataBlob { Size = cipher.Length, Data = Marshal.AllocHGlobal(cipher.Length) };
        try
        {
            Marshal.Copy(cipher, 0, input.Data, cipher.Length);
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UIForbidden, out var output))
                throw new InvalidOperationException("CryptUnprotectData falhou: " + Marshal.GetLastWin32Error());
            try { return Copy(output); }
            finally { if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }

    static byte[] Copy(DataBlob b)
    {
        var buf = new byte[b.Size];
        if (b.Size > 0) Marshal.Copy(b.Data, buf, 0, b.Size);
        return buf;
    }
}
