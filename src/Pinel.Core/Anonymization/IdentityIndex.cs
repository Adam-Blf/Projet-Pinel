using Pinel.Core.Formats;

namespace Pinel.Core.Anonymization;

/// <summary>
/// Index des identifiants vus en entree, construit avant toute ecriture.
/// </summary>
/// <remarks>
/// <para>
/// Deux usages. D'abord rattacher chaque ligne a un patient, pour que toutes
/// ses dates bougent du meme decalage : une ligne FICHCOMP ne porte que le
/// numero de sejour, le RPS du meme sejour porte l'IPP, l'index fait le lien.
/// </para>
/// <para>
/// Ensuite servir de reference au controle de fuite : toute valeur
/// d'identifiant d'au moins <see cref="MinimumLength"/> caracteres retrouvee
/// dans un champ recopie tel quel est une fuite.
/// </para>
/// <para>
/// L'index ne vit qu'en memoire, le temps de l'execution. Il n'est jamais
/// ecrit sur disque.
/// </para>
/// </remarks>
public sealed class IdentityIndex
{
    public const int MinimumLength = 8;

    private readonly Dictionary<string, string> _patientOfStay = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _patientOfNir = new(StringComparer.Ordinal);
    private readonly HashSet<string> _identifiers = new(StringComparer.Ordinal);
    private readonly SortedSet<int> _lengths = new();

    public IReadOnlyCollection<string> Identifiers => _identifiers;

    public static IdentityIndex Build(IEnumerable<(string Path, RecordSpec Spec)> files)
    {
        var index = new IdentityIndex();
        foreach (var (path, spec) in files)
        {
            var roles = spec.FixedFields.Select((f, i) => (Field: f, Role: spec.FixedRoles[i]))
                .Where(t => t.Role is FieldRole.PatientId or FieldRole.StayId or FieldRole.Nir or FieldRole.ChainingKey)
                .ToList();
            if (roles.Count == 0) continue;

            foreach (var line in File.ReadLines(path, PmsiEncoding.Latin1))
            {
                string? patient = null;
                var stays = new List<string>();
                var nirs = new List<string>();
                foreach (var (field, role) in roles)
                {
                    var value = field.Read(line);
                    if (value.Length == 0) continue;
                    index.Remember(value);
                    if (role == FieldRole.PatientId) patient ??= value;
                    else if (role == FieldRole.StayId) stays.Add(value);
                    else if (role == FieldRole.Nir) nirs.Add(value);
                }
                if (patient is null) continue;
                foreach (var stay in stays) index._patientOfStay.TryAdd(stay, patient);
                foreach (var nir in nirs) index._patientOfNir.TryAdd(nir, patient);
            }
        }
        return index;
    }

    /// <summary>
    /// Cle du patient d'une ligne : l'IPP s'il est present, sinon celui que
    /// l'index associe a son numero de sejour ou a son NIR, sinon le numero
    /// lui-meme.
    /// </summary>
    public string PatientKey(string line, RecordSpec spec)
    {
        string? stay = null, nir = null;
        for (int i = 0; i < spec.FixedFields.Count; i++)
        {
            var role = spec.FixedRoles[i];
            if (role is not (FieldRole.PatientId or FieldRole.StayId or FieldRole.Nir)) continue;
            var value = spec.FixedFields[i].Read(line);
            if (value.Length == 0) continue;
            if (role == FieldRole.PatientId) return value;
            if (role == FieldRole.StayId) stay ??= value;
            else nir ??= value;
        }
        if (stay is not null && _patientOfStay.TryGetValue(stay, out var p1)) return p1;
        if (nir is not null && _patientOfNir.TryGetValue(nir, out var p2)) return p2;
        return stay ?? nir ?? string.Empty;
    }

    /// <summary>Vrai si <paramref name="text"/> contient un identifiant connu.</summary>
    public bool ContainsIdentifier(string text)
    {
        foreach (var length in _lengths)
        {
            if (length > text.Length) break;
            for (int i = 0; i + length <= text.Length; i++)
            {
                if (_identifiers.Contains(text.Substring(i, length))) return true;
            }
        }
        return false;
    }

    private void Remember(string value)
    {
        // Une valeur faite d'un seul caractere repete (zeros de remplissage)
        // n'identifie personne et ferait crier le controle partout.
        if (value.Length < MinimumLength || value.All(c => c == value[0])) return;
        if (_identifiers.Add(value)) _lengths.Add(value.Length);
    }
}
