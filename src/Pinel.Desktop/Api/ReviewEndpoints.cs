using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pinel.Core.Formats;
using Pinel.Core.Learning;
using Pinel.Ml;
using Pinel.Core.Security;

namespace Pinel.Desktop.Api;

/// <summary>
/// Points de terminaison de l'écran de revue : règles apprises des corrections
/// du DIM, décision du DIM sur chacune, et lignes que le modèle signale.
/// </summary>
/// <remarks>
/// Aucune de ces routes ne modifie un fichier destiné à l'ATIH. Une règle
/// validée ici ne fait qu'entrer dans les suggestions ; la correction d'un lot
/// reste une action explicite, lancée depuis l'écran de conversion.
/// </remarks>
internal static class ReviewEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/api/revue/regles", (PinelSession session) =>
        {
            var store = RuleStore.Load(ReviewPaths.RulesFile);
            return Results.Ok(new
            {
                fichier = ReviewPaths.RulesFile,
                regles = store.Rules.Select((rule, index) => new
                {
                    numero = index + 1,
                    format = rule.Format,
                    libelle = rule.Describe(),
                    confiance = Math.Round(rule.Confidence, 3),
                    cas = rule.Support,
                    contradictions = rule.Contradictions,
                    statut = rule.Status.ToString(),
                    suggeree = RuleApplier.IsSuggested(rule),
                    vueLe = rule.LastSeen.ToString("dd/MM/yyyy"),
                }),
            });
        });

        app.MapPost("/api/revue/regles/statut", (RuleDecisionRequest r) =>
        {
            var store = RuleStore.Load(ReviewPaths.RulesFile);
            if (r.Numero < 1 || r.Numero > store.Rules.Count)
            {
                return Results.NotFound(new { error = "règle inconnue" });
            }
            var status = r.Decision?.ToLowerInvariant() switch
            {
                "valider" => RuleStatus.Validee,
                "rejeter" => RuleStatus.Rejetee,
                "proposer" => RuleStatus.Proposee,
                _ => (RuleStatus?)null,
            };
            if (status is null) return Results.BadRequest(new { error = "décision inconnue" });

            store.Rules[r.Numero - 1].Status = status.Value;
            store.Save(ReviewPaths.RulesFile);
            return Results.Ok(new { statut = status.Value.ToString() });
        });

        app.MapPost("/api/revue/suggestions", (PinelSession session) =>
        {
            var store = RuleStore.Load(ReviewPaths.RulesFile);
            var specs = Specs(session);
            var applier = new RuleApplier(store.Rules, specs);

            var hits = session.Settings.Folders
                .Where(Directory.Exists)
                .SelectMany(folder => applier.Run(folder))
                .GroupBy(h => h.Rule)
                .Select(g => new
                {
                    libelle = g.Key.Describe(),
                    statut = g.Key.Status.ToString(),
                    lignes = g.Sum(h => h.Lines),
                    fichiers = g.Select(h => h.RelativePath).Distinct().Count(),
                })
                .OrderByDescending(h => h.lignes)
                .ToList();

            return Results.Ok(new { total = hits.Sum(h => h.lignes), regles = hits });
        });

        app.MapGet("/api/revue/modele", () =>
        {
            if (!File.Exists(Path.Combine(ReviewPaths.ModelFolder, "modele.json")))
            {
                return Results.Ok(new { present = false });
            }
            var (_, card) = DeletionModel.Load(ReviewPaths.ModelFolder);
            return Results.Ok(new
            {
                present = true,
                entraineLe = card.TrainedAt.ToString("dd/MM/yyyy"),
                moisAppris = card.TrainingMonths,
                moisDeControle = card.HoldoutMonth,
                auprc = Math.Round(card.Auprc, 3),
                suppressionsDuControle = card.HoldoutDeletions,
                lignesDuControle = card.HoldoutLines,
            });
        });

        app.MapPost("/api/revue/lignes-a-revoir", (PinelSession session) =>
        {
            if (!File.Exists(Path.Combine(ReviewPaths.ModelFolder, "modele.json")))
            {
                return Results.BadRequest(new { error = "Aucun modèle entraîné sur ce poste." });
            }

            var raa = Specs(session).FirstOrDefault(s => s.Format == "RAA");
            if (raa is null) return Results.BadRequest(new { error = "Descriptif RAA absent." });

            var (engine, card) = DeletionModel.Load(ReviewPaths.ModelFolder);
            var builder = new RaaFeatureBuilder(raa);
            var detector = new ContentFormatDetector(Specs(session));

            var files = new List<object>();
            foreach (var folder in session.Settings.Folders.Where(Directory.Exists))
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories))
                {
                    if (detector.Detect(path).Spec?.Format != "RAA") continue;
                    var lines = File.ReadAllLines(path, PmsiEncoding.Latin1);
                    var flagged = builder.Build(lines)
                        .Select((example, index) => (Ligne: index + 1, Score: engine.Predict(example).Probabilite))
                        .Where(s => s.Score >= card.Threshold)
                        .OrderByDescending(s => s.Score)
                        .Take(200)
                        .ToList();
                    if (flagged.Count == 0) continue;
                    files.Add(new
                    {
                        fichier = Path.GetFileName(path),
                        lignes = lines.Length,
                        signalees = flagged.Count,
                        premieres = flagged.Take(20).Select(s => new { s.Ligne, score = Math.Round(s.Score, 3) }),
                    });
                }
            }
            return Results.Ok(new { seuil = Math.Round(card.Threshold, 3), fichiers = files });
        });
    }

    private static IReadOnlyList<RecordSpec> Specs(PinelSession session) =>
        session.Layouts.Formats
            .Select(f => session.Layouts.Resolve(f))
            .OfType<FormatLayout>()
            .Select(RecordSpec.For)
            .ToList();
}

/// <summary>Décision du DIM sur une règle, par son numéro d'affichage.</summary>
public sealed record RuleDecisionRequest(int Numero, string? Decision);

/// <summary>
/// Emplacements des règles apprises et du modèle : sous le profil de
/// l'utilisateur, jamais dans le dossier d'installation ni dans un dossier
/// partagé. Ils portent des codes métier, jamais de donnée patient.
/// </summary>
internal static class ReviewPaths
{
    public static string RulesFile => PinelPaths.In("regles-apprises.json");

    public static string ModelFolder => PinelPaths.In("modele");
}
