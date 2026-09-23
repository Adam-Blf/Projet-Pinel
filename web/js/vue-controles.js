/*
 * Pinel - écran "Contrôles qualité" : anomalies relevées sur le lot avant
 * transmission aux tutelles (FINESS, dates, chaînage, doublons, année).
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  async function runChecks() {
    var result = await api.call('/api/controles', { method: 'POST' });
    var summary = result.synthese || {};
    var anomalies = result.anomalies || [];

    var counters = dom.kpiCards(Object.keys(summary).map(function (level) { return [level, summary[level]]; }));

    var rows = anomalies.slice(0, 200).map(function (finding) {
      return '<tr><td>' + dom.escapeHtml(finding.sourceFile || '') + '</td>' +
        '<td class="num">' + dom.escapeHtml(finding.lineNumber || '') + '</td>' +
        '<td>' + dom.escapeHtml(finding.code || '') + '</td>' +
        '<td>' + dom.escapeHtml(finding.message || '') + '</td></tr>';
    }).join('');

    dom.$('card-controles').innerHTML =
      '<h3>Résultat</h3><div class="grid cols-4">' + counters + '</div>' +
      (anomalies.length
        ? '<div class="table-wrap mt-3"><table><thead><tr><th>Fichier</th>' +
          '<th class="num">Ligne</th><th>Code</th><th>Anomalie</th></tr></thead><tbody>' + rows + '</tbody></table></div>' +
          (anomalies.length > 200
            ? '<p class="hint mt-3">Affichage limité aux 200 premières anomalies sur ' + anomalies.length + '.</p>'
            : '')
        : '<div class="note mt-3">Aucune anomalie sur le lot.</div>');
    dom.toast(anomalies.length + ' anomalie(s).');
  }

  function wire() {
    dom.$('btn-controles').addEventListener('click', api.guard(runChecks));
  }

  wire();
})();
