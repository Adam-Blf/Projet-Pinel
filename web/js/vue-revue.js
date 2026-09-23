/*
 * Pinel - écran "Revue des corrections" : les règles tirées des corrections
 * que le DIM refait à la main, la décision du DIM sur chacune, et les lignes
 * que le modèle signale.
 *
 * Rien n'est appliqué ici. Valider une règle la fait entrer dans les
 * suggestions des lots suivants ; corriger un lot reste une action séparée,
 * lancée depuis l'écran de conversion.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api, window.Pinel.nav.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  var ETATS = {
    Validee: ['badge ok', 'Validée'],
    Rejetee: ['badge warn', 'Rejetée'],
    Proposee: ['badge', 'Proposée']
  };

  function renderRules(rules) {
    var body = dom.$('tbody-regles');
    dom.$('badge-revue').textContent = rules.filter(function (r) { return r.statut === 'Proposee'; }).length;

    dom.kpis('kpi-revue', [
      ['Règles apprises', rules.length],
      ['Validées', rules.filter(function (r) { return r.statut === 'Validee'; }).length],
      ['À trancher', rules.filter(function (r) { return r.statut === 'Proposee'; }).length],
      ['Suggérées', rules.filter(function (r) { return r.suggeree; }).length]
    ]);

    if (!rules.length) {
      body.innerHTML = '<tr><td colspan="5" class="empty">Aucune règle apprise pour l\'instant. ' +
        'Elles apparaissent après un apprentissage sur vos fichiers corrigés.</td></tr>';
      return;
    }

    body.innerHTML = rules.map(function (rule) {
      var etat = ETATS[rule.statut] || ETATS.Proposee;
      return '<tr><td>' + dom.escapeHtml(rule.libelle) + '</td>' +
        '<td class="num">' + Math.round(rule.confiance * 100) + ' %</td>' +
        '<td class="num">' + dom.escapeHtml(rule.cas) + '</td>' +
        '<td><span class="' + etat[0] + '">' + etat[1] + '</span></td>' +
        '<td class="align-end">' +
        '<button class="btn secondary small" data-decision="valider" data-numero="' + rule.numero + '" type="button">Valider</button> ' +
        '<button class="btn ghost small" data-decision="rejeter" data-numero="' + rule.numero + '" type="button">Rejeter</button>' +
        '</td></tr>';
    }).join('');

    dom.els('[data-decision]', body).forEach(function (button) {
      button.addEventListener('click', api.guard(async function () {
        await api.call('/api/revue/regles/statut', {
          method: 'POST',
          body: { numero: parseInt(button.dataset.numero, 10), decision: button.dataset.decision }
        });
        dom.toast('Règle ' + button.dataset.numero + ' ' +
          (button.dataset.decision === 'valider' ? 'validée.' : 'rejetée.'));
        await load();
      }));
    });
  }

  async function load() {
    var result = await api.call('/api/revue/regles');
    renderRules(result.regles || []);

    var model = await api.call('/api/revue/modele');
    dom.$('revue-modele').textContent = model.present
      ? 'Modèle entraîné le ' + model.entraineLe + ' sur ' + (model.moisAppris || []).join(', ') +
        ', contrôlé sur ' + model.moisDeControle + ' qui ne lui avait pas servi : ' +
        model.suppressionsDuControle + ' suppressions sur ' + model.lignesDuControle + ' lignes retrouvées, ' +
        'qualité mesurée ' + model.auprc + ' sur 1.'
      : "Aucun modèle entraîné sur ce poste. Il s'entraîne sur vos fichiers d'origine et corrigés.";
  }

  async function suggestions() {
    var result = await api.call('/api/revue/suggestions', { method: 'POST' });
    var rows = (result.regles || []).map(function (r) {
      return '<tr><td>' + dom.escapeHtml(r.libelle) + '</td>' +
        '<td>' + dom.escapeHtml(r.statut) + '</td>' +
        '<td class="num">' + dom.escapeHtml(r.fichiers) + '</td>' +
        '<td class="num">' + dom.escapeHtml(r.lignes) + '</td></tr>';
    }).join('');

    dom.$('card-suggestions').innerHTML =
      '<h3>Lignes concernées dans les dossiers de travail</h3>' +
      (rows
        ? '<div class="table-wrap"><table><thead><tr><th>Correction</th><th>État</th>' +
          '<th class="num">Fichiers</th><th class="num">Lignes</th></tr></thead><tbody>' + rows + '</tbody></table></div>'
        : '<div class="empty">Aucune règle ne concerne les fichiers des dossiers de travail.</div>');
    dom.toast(result.total + ' ligne(s) concernée(s).');
  }

  async function flaggedLines() {
    var result = await api.call('/api/revue/lignes-a-revoir', { method: 'POST' });
    var files = result.fichiers || [];
    if (!files.length) {
      dom.$('card-lignes-a-revoir').innerHTML =
        '<div class="note mt-3">Aucune ligne au-dessus du seuil dans les dossiers de travail.</div>';
      dom.toast('Aucune ligne signalée.');
      return;
    }

    dom.$('card-lignes-a-revoir').innerHTML =
      '<div class="table-wrap mt-3"><table><thead><tr><th>Fichier</th><th class="num">Lignes</th>' +
      '<th class="num">Signalées</th><th>Premiers numéros de ligne</th></tr></thead><tbody>' +
      files.map(function (f) {
        var numeros = f.premieres.map(function (p) { return p.Ligne; }).join(', ');
        return '<tr><td>' + dom.escapeHtml(f.fichier) + '</td>' +
          '<td class="num">' + dom.escapeHtml(f.lignes) + '</td>' +
          '<td class="num">' + dom.escapeHtml(f.signalees) + '</td>' +
          '<td class="path">' + dom.escapeHtml(numeros) + '</td></tr>';
      }).join('') + '</tbody></table></div>';
    dom.toast(files.reduce(function (n, f) { return n + f.signalees; }, 0) + ' ligne(s) à revoir.');
  }

  function wire() {
    dom.$('btn-revue-recharger').addEventListener('click', api.guard(load));
    dom.$('btn-revue-suggestions').addEventListener('click', api.guard(suggestions));
    dom.$('btn-revue-lignes').addEventListener('click', api.guard(flaggedLines));

    window.addEventListener('pinel:view', function (event) {
      if (event.detail === 'revue') load().catch(function (e) { dom.toast('Erreur : ' + e.message, 'error'); });
    });
  }

  wire();
  dom.kpis('kpi-revue', [['Règles apprises', 0], ['Validées', 0], ['À trancher', 0], ['Suggérées', 0]]);
})();
