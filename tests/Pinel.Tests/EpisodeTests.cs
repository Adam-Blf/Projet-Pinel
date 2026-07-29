using Pinel.Core.Episodes;

namespace Pinel.Tests;

/// <summary>
/// Chantier 2 du cahier des charges : regrouper les journées consécutives en
/// épisodes, par patient, UM et site, pour mesurer une durée de présence.
/// </summary>
public sealed class EpisodeTests
{
    private static AmbulatoryVisit Visit(string ipp, string date, string um = "UM01", string site = "FV94") =>
        new(ipp, DateOnly.ParseExact(date, "yyyy-MM-dd"), um, site);

    [Fact]
    public void Regroupe_les_journees_consecutives_en_un_seul_episode()
    {
        var episodes = EpisodeBuilder.Build(new[]
        {
            Visit("1", "2025-03-10"),
            Visit("1", "2025-03-11"),
            Visit("1", "2025-03-12"),
        });

        var episode = Assert.Single(episodes);
        Assert.Equal(new DateOnly(2025, 3, 10), episode.Start);
        Assert.Equal(new DateOnly(2025, 3, 12), episode.End);
        Assert.Equal(3, episode.VisitCount);
        Assert.Equal(2, episode.DurationDays);
    }

    [Fact]
    public void Une_venue_isolee_a_une_duree_nulle()
    {
        var episode = Assert.Single(EpisodeBuilder.Build(new[] { Visit("1", "2025-03-10") }));
        Assert.Equal(0, episode.DurationDays);
    }

    [Fact]
    public void Un_jour_sans_venue_ouvre_un_nouvel_episode()
    {
        var episodes = EpisodeBuilder.Build(new[]
        {
            Visit("1", "2025-03-10"),
            Visit("1", "2025-03-12"),
        });

        Assert.Equal(2, episodes.Count);
    }

    [Fact]
    public void La_tolerance_permet_de_recoller_deux_journees_espacees()
    {
        var episodes = EpisodeBuilder.Build(
            new[] { Visit("1", "2025-03-10"), Visit("1", "2025-03-12") },
            new EpisodeRules(MaxGapDays: 1));

        Assert.Single(episodes);
    }

    [Fact]
    public void Un_episode_ne_franchit_jamais_la_fin_d_annee()
    {
        // Règle retenue par le DIM : le recueil est annuel, l'épisode se ferme
        // au 31 décembre même si le patient revient le 1er janvier.
        var episodes = EpisodeBuilder.Build(new[]
        {
            Visit("1", "2025-12-31"),
            Visit("1", "2026-01-01"),
        });

        Assert.Equal(2, episodes.Count);
        Assert.Equal(new DateOnly(2025, 12, 31), episodes[0].End);
        Assert.Equal(new DateOnly(2026, 1, 1), episodes[1].Start);
    }

    [Fact]
    public void Le_changement_d_um_ouvre_un_episode_distinct()
    {
        var episodes = EpisodeBuilder.Build(new[]
        {
            Visit("1", "2025-03-10", um: "UM01"),
            Visit("1", "2025-03-11", um: "UM02"),
        });

        Assert.Equal(2, episodes.Count);
    }

    [Fact]
    public void Deux_venues_le_meme_jour_ne_comptent_qu_une_fois()
    {
        var episode = Assert.Single(EpisodeBuilder.Build(new[]
        {
            Visit("1", "2025-03-10"),
            Visit("1", "2025-03-10"),
        }));

        Assert.Equal(1, episode.VisitCount);
    }

    [Fact]
    public void L_identifiant_d_episode_est_stable_entre_deux_executions()
    {
        var visits = new[] { Visit("123", "2025-03-10", "UM01", "FV94") };
        var first = EpisodeBuilder.Build(visits)[0].EpisodeId;
        var second = EpisodeBuilder.Build(visits)[0].EpisodeId;

        Assert.Equal(first, second);
        Assert.Equal("123-FV94-UM01-20250310", first);
    }

    [Theory]
    [InlineData("20250310", 2025, 3, 10)]
    [InlineData("10032025", 2025, 3, 10)]
    public void Les_deux_conventions_de_date_du_recueil_sont_acceptees(string value, int y, int m, int d)
    {
        Assert.Equal(new DateOnly(y, m, d), EpisodeBuilder.ParseDate(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2025")]
    [InlineData("99999999")]
    public void Une_date_impossible_est_rejetee_plutot_que_devinee(string value)
    {
        Assert.Null(EpisodeBuilder.ParseDate(value));
    }

    [Theory]
    [InlineData("01021201")]  // an 102 en AAAAMMJJ, 1201 en JJMMAAAA : absurde des deux cotes
    [InlineData("00010101")]
    public void Une_date_hors_plage_plausible_est_rejetee(string value)
    {
        Assert.Null(EpisodeBuilder.ParseDate(value));
    }

    [Fact]
    public void La_convention_attendue_est_respectee_quand_elle_est_precisee()
    {
        // 01022025 vaut le 1er fevrier 2025 en JJMMAAAA. En AAAAMMJJ, l'annee
        // 0102 sort de la plage plausible, la lecture bascule donc seule.
        Assert.Equal(new DateOnly(2025, 2, 1),
            EpisodeBuilder.ParseDate("01022025", DateConvention.DayFirst));
        Assert.Equal(new DateOnly(2025, 2, 1), EpisodeBuilder.ParseDate("01022025"));
    }

    [Fact]
    public void Deux_codes_um_distincts_ne_produisent_pas_le_meme_identifiant()
    {
        // UM-01 et UM01 sont deux unites differentes : leurs episodes ne
        // doivent pas se confondre dans les jointures en aval.
        var premier = EpisodeBuilder.Build(new[] { Visit("1", "2025-03-10", um: "UM-01") })[0];
        var second = EpisodeBuilder.Build(new[] { Visit("1", "2025-03-10", um: "UM01") })[0];

        Assert.NotEqual(premier.EpisodeId, second.EpisodeId);
    }

    [Fact]
    public void La_regle_appliquee_est_decrite_en_clair_pour_le_dim()
    {
        var description = EpisodeRules.Default.Describe();
        Assert.Contains("consecutives", description);
        Assert.Contains("31 decembre", description);
    }
}
