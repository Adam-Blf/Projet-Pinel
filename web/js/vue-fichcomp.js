/*
 * Pinel - écran "Transports et fichiers complémentaires" : nettoyage d'un
 * classeur transports (l'ancienne moulinette Excel FICHCOMP transports du
 * DIM) et contrôle d'un fichier complémentaire à largeur fixe.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  async function cleanWorkbook() {
    var file = await api.pickFile('Classeur Excel|*.xlsx;*.xlsm|Tous les fichiers|*.*');
    if (!file) return;
    var result = await api.call('/api/fichcomp/nettoyer', { method: 'POST', body: { fichier: file } });
    dom.$('card-fichcomp').innerHTML =
      '<h3>Classeur nettoyé</h3>' +
      '<p class="path">' + dom.escapeHtml(result.sortie) + '</p>' +
      '<div class="grid cols-4">' + dom.kpiCards([
        ['Lignes lues', result.lignesLues],
        ['En-têtes retirés', result.lignesRetirees],
        ['Dates complétées', result.datesCompletees]
      ]) + '</div>';
    dom.toast('Classeur transports nettoyé.');
  }

  async function checkFichcomp() {
    var file = await api.pickFile('Fichier complémentaire|*.txt|Tous les fichiers|*.*');
    if (!file) return;
    var result = await api.call('/api/fichcomp/controler', {
      method: 'POST',
      body: { fichier: file, type: dom.$('fichcomp-type').value }
    });

    var rows = (result.anomalies || []).map(function (issue) {
      return '<tr><td class="num">' + dom.escapeHtml(issue.lineNumber) + '</td><td>' + dom.escapeHtml(issue.message) + '</td></tr>';
    }).join('');

    dom.$('card-fichcomp').innerHTML =
      '<h3>' + dom.escapeHtml(result.type) + '</h3>' +
      '<p class="hint">' + dom.escapeHtml(result.lignes) + ' ligne(s) contrôlée(s).</p>' +
      (result.conforme
        ? '<div class="note">Aucune anomalie détectée.</div>'
        : '<div class="table-wrap"><table><thead><tr><th class="num">Ligne</th><th>Anomalie</th></tr></thead><tbody>' +
          rows + '</tbody></table></div>');
    dom.toast(result.conforme ? 'Fichier conforme.' : 'Anomalies détectées.');
  }

  function wire() {
    dom.$('btn-fichcomp-nettoyer').addEventListener('click', api.guard(cleanWorkbook));
    dom.$('btn-fichcomp-controler').addEventListener('click', api.guard(checkFichcomp));
  }

  wire();
})();
