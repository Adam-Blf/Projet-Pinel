/*
 * Pinel - écran "Structure du GHT" : lecture du fichier de structure FICOM
 * et répartition par type de secteur ARS.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  var SECTOR_LABELS = { G: 'Adulte', I: 'Infanto-juvénile', D: 'UMD', P: 'UHSA', Z: 'Intersectoriel' };

  async function loadStructure() {
    var file = await api.pickFile('Structure|*.csv;*.tsv;*.txt|Tous les fichiers|*.*');
    if (!file) return;
    var result = await api.call('/api/structure', { method: 'POST', body: { fichier: file } });
    var summary = result.summary || {};
    var sectors = summary.bySectorType || {};

    var rows = Object.keys(sectors).map(function (key) {
      var label = SECTOR_LABELS[key] || 'Non renseigné';
      return '<tr><td><span class="badge">' + dom.escapeHtml(key) + '</span> ' + dom.escapeHtml(label) +
        '</td><td class="num">' + dom.escapeHtml(sectors[key]) + '</td></tr>';
    }).join('');

    dom.$('card-structure').innerHTML =
      '<h3>' + dom.escapeHtml(result.filename) + '</h3>' +
      '<p class="hint">Colonnes reconnues : ' + dom.escapeHtml((result.headers || []).join(', ')) + '</p>' +
      '<div class="grid cols-4">' +
      '<div class="kpi"><div class="label">Unités</div><div class="value">' + (summary.totalNodes || 0) + '</div></div>' +
      '<div class="kpi"><div class="label">Racines</div><div class="value">' + (summary.roots || 0) + '</div></div>' +
      '<div class="kpi"><div class="label">Profondeur</div><div class="value">' + (summary.maxDepth || 0) + '</div></div>' +
      '</div>' +
      '<div class="table-wrap" style="margin-top:var(--s3)"><table>' +
      '<thead><tr><th>Type de secteur ARS</th><th class="num">Unités</th></tr></thead>' +
      '<tbody>' + (rows || '<tr><td colspan="2" class="empty">Aucun secteur ARS identifié dans le fichier.</td></tr>') +
      '</tbody></table></div>' +
      '<div class="row" style="margin-top:var(--s3)">' +
      '<button class="btn secondary" id="btn-structure-export" type="button">Exporter la structure à plat</button>' +
      '</div>';

    var exportButton = dom.$('btn-structure-export');
    if (exportButton) {
      exportButton.addEventListener('click', api.guard(async function () {
        var out = await api.call('/api/structure/export', { method: 'POST', body: { fichier: file } });
        dom.toast(out.lignes + ' unité(s) exportée(s) vers ' + out.sortie);
      }));
    }
    dom.toast('Structure chargée.');
  }

  function wire() {
    dom.$('btn-structure').addEventListener('click', api.guard(loadStructure));
  }

  wire();
})();
