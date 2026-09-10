/*
 * Pinel - écran "Conversion des formats ATIH" : ajout de dossiers de
 * travail, détection des fichiers reconnus, traitement du lot, export CSV.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  function renderFiles(files) {
    var body = dom.$('tbody-fichiers');
    if (!files.length) {
      body.innerHTML = '<tr><td colspan="4" class="empty">' +
        'Aucun fichier trouvé dans les dossiers de travail. Vérifiez qu\'ils contiennent des fichiers ATIH ' +
        'ou ajoutez un autre dossier.</td></tr>';
      return;
    }
    body.innerHTML = files.map(function (file) {
      var known = file.format && file.format !== 'INCONNU';
      return '<tr><td>' + dom.escapeHtml(file.name) + '</td>' +
        '<td><span class="badge ' + (known ? 'ok' : 'warn') + '">' + dom.escapeHtml(file.format) + '</span></td>' +
        '<td class="num">' + dom.escapeHtml(file.sizeKb) + '</td>' +
        '<td class="path">' + dom.escapeHtml(file.dir) + '</td></tr>';
    }).join('');
    dom.$('badge-fichiers').textContent = files.length;
  }

  async function addFolderAndScan() {
    var folder = await api.pickFolder();
    if (!folder) return;
    await api.call('/api/reglages/dossier', { method: 'POST', body: { chemin: folder } });
    dom.toast('Dossier ajouté : ' + folder);
    await scan();
  }

  async function scan() {
    var result = await api.call('/api/scanner', { method: 'POST' });
    renderFiles(result.fichiers || []);
    dom.kpis('kpi-moulinette', [
      ['Fichiers', result.total || 0], ['Lignes lues', '—'], ['IPP uniques', '—'], ['Conflits', '—']
    ]);
    dom.toast(result.total + ' fichier(s) détecté(s).');
  }

  async function process() {
    dom.toast('Traitement en cours…');
    var totals = await api.call('/api/traiter', { method: 'POST' });
    dom.kpis('kpi-moulinette', [
      ['Fichiers traités', totals.filesProcessed],
      ['Lignes lues', totals.linesValid],
      ['IPP uniques', totals.ippUnique],
      ['Conflits', totals.collisions]
    ]);
    dom.$('badge-conflits').textContent = totals.collisions;
    dom.toast('Traitement terminé : ' + totals.linesValid + ' ligne(s).');
  }

  async function exportCsv() {
    var result = await api.call('/api/export-csv', { method: 'POST' });
    var files = result.fichiers || [];
    var raw = files.filter(function (f) { return f.brut; }).length;
    dom.toast(files.length + ' CSV écrit(s) dans ' + result.dossier +
      (raw ? ' - ' + raw + ' sans descriptif, exporté(s) en ligne brute' : ''));
  }

  function reset() {
    renderFiles([]);
    dom.$('badge-fichiers').textContent = '0';
    dom.$('badge-conflits').textContent = '0';
    dom.kpis('kpi-moulinette', [['Fichiers', 0], ['Lignes lues', 0], ['IPP uniques', 0], ['Conflits', 0]]);
  }

  function wire() {
    dom.$('btn-ajouter-dossier').addEventListener('click', api.guard(addFolderAndScan));
    dom.$('btn-scanner').addEventListener('click', api.guard(scan));
    dom.$('btn-traiter').addEventListener('click', api.guard(process));
    dom.$('btn-export-csv').addEventListener('click', api.guard(exportCsv));
  }

  wire();
  dom.kpis('kpi-moulinette', [['Fichiers', 0], ['Lignes lues', 0], ['IPP uniques', 0], ['Conflits', 0]]);

  window.Pinel.moulinette = { reset: reset };
})();
