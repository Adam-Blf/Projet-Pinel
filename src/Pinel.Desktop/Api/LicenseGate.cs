using Microsoft.AspNetCore.Http;
using Pinel.Core.Licensing;

namespace Pinel.Desktop.Api;

/// <summary>
/// Porte de licence des operations qui ecrivent un fichier.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui est bride et ce qui ne l'est pas, et pourquoi. Sans licence valable,
/// Pinel continue de lire les fichiers, de les reconnaitre et de passer ses
/// controles : un departement d'information medicale ne doit jamais perdre la
/// vue sur ses propres donnees a cause d'une echeance commerciale. Ce sont les
/// ecritures qui s'arretent, c'est-a-dire ce qui fait gagner du temps :
/// exports, classeurs nettoyes, copies corrigees.
/// </para>
/// <para>
/// La tolerance de trente jours apres l'echeance laisse passer une periode de
/// transmission entiere, le temps qu'un bon de commande suive.
/// </para>
/// </remarks>
internal static class LicenseGate
{
    /// <summary>
    /// Rend null quand l'ecriture est autorisee, sinon la reponse a renvoyer.
    /// </summary>
    public static IResult? Refuse(PinelSession session)
    {
        var status = LicenseVerifier.Check(session.Settings.Etablissement.FinessEPmsi);
        if (status.Valid) return null;

        return Results.Json(new
        {
            error = status.Reason + " Les écritures de fichiers sont suspendues ; la lecture et les contrôles restent disponibles.",
            licence = status,
        }, statusCode: StatusCodes.Status402PaymentRequired);
    }
}
