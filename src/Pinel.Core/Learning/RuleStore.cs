using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinel.Core.Learning;

/// <summary>
/// Memoire des regles apprises, d'un mois sur l'autre.
/// </summary>
/// <remarks>
/// <para>
/// C'est ce fichier qui fait que Pinel s'ameliore avec le temps : chaque
/// nouvelle paire origine / corrige ajoute ses observations aux regles deja
/// connues. Une regle confirmee mois apres mois voit sa confiance monter, une
/// regle que le DIM cesse d'appliquer la voit baisser.
/// </para>
/// <para>
/// Une meme paire de fichiers n'est comptee qu'une fois, quel que soit le
/// nombre de copies qu'en garde l'arborescence (les essais successifs d'un
/// meme mois recopient souvent les memes fichiers) : l'empreinte de chaque
/// paire deja apprise est conservee.
/// </para>
/// <para>
/// Le statut decide par le DIM (validee, rejetee) n'est jamais modifie par un
/// nouvel apprentissage.
/// </para>
/// </remarks>
public sealed class RuleStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public List<LearnedRule> Rules { get; set; } = new();
    public HashSet<string> LearnedPairs { get; set; } = new(StringComparer.Ordinal);

    public static RuleStore Load(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<RuleStore>(File.ReadAllText(path), Json) ?? new RuleStore()
            : new RuleStore();

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Empreinte d'une paire de fichiers, sur leur contenu.</summary>
    public static string Fingerprint(string original, string corrected)
    {
        using var sha = SHA256.Create();
        foreach (var path in new[] { original, corrected })
        {
            using var stream = File.OpenRead(path);
            var digest = SHA256.HashData(stream);
            sha.TransformBlock(digest, 0, digest.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    /// <summary>
    /// Ajoute les observations d'une paire. Rend faux si la paire avait deja
    /// ete apprise.
    /// </summary>
    public bool Merge(string fingerprint, IEnumerable<LearnedRule> observed)
    {
        if (!LearnedPairs.Add(fingerprint)) return false;
        var byKey = Rules.ToDictionary(r => r.Key, StringComparer.Ordinal);
        foreach (var rule in observed)
        {
            if (byKey.TryGetValue(rule.Key, out var known))
            {
                known.Support += rule.Support;
                known.Contradictions += rule.Contradictions;
                if (rule.LastSeen > known.LastSeen) known.LastSeen = rule.LastSeen;
                if (rule.FirstSeen < known.FirstSeen) known.FirstSeen = rule.FirstSeen;
            }
            else
            {
                Rules.Add(rule);
                byKey[rule.Key] = rule;
            }
        }
        return true;
    }
}
