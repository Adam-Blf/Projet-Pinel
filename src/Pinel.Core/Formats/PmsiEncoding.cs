using System.Text;

namespace Pinel.Core.Formats;

/// <summary>
/// Encodage des fichiers ATIH : ISO-8859-1, un caractere par octet. C'est ce
/// qui garantit que la position d'un champ en caracteres est aussi sa position
/// en octets.
/// </summary>
public static class PmsiEncoding
{
    public static readonly Encoding Latin1 = Encoding.Latin1;
}
