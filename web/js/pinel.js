/*
 * Pinel - logique d'interface.
 *
 * Aucune dépendance : le service HTTP local est appelé en fetch, les icônes
 * Icons8 sont posées en masque CSS, les boîtes de dialogue Windows passent par
 * le message natif de la fenêtre hôte.
 */
(function () {
  'use strict';

  var BRIDGE = 'http://127.0.0.1:8787';
  var TOKEN = window.__PINEL_TOKEN || null;

  var VIEWS = {
    moulinette: ['Conversion des formats ATIH',
      'Cahier des charges DIM, point 1 - fichiers au format national convertis en CSV exploitable'],
    episodes: ['Épisodes de prise en charge',
      'Cahier des charges DIM, point 2 - regroupement des journées consécutives en ambulatoire'],
    structure: ['Structure du GHT',
      'Cahier des charges DIM, point 3 - lecture et contrôle du fichier de structure FICOM'],
    fichcomp: ['Fichiers complémentaires',
      'Classeurs transports et fichiers complémentaires à largeur fixe'],
    identito: ['Identitovigilance',
      'Identifiants patients porteurs de plusieurs dates de naissance'],
    controles: ['Contrôles qualité',
      'Anomalies relevées avant transmission aux tutelles'],
    dossiers: ['Emplacements autorisés',
      'Répertoires que Pinel est autorisé à lire et à écrire'],
    journal: ['Traçabilité',
      "Journal des opérations sensibles, registre au titre de l'article 30 du RGPD"]
  };

  // ── Utilitaires ───────────────────────────────────────────────────────
  function $(id) { return document.getElementById(id); }
  function el(sel, root) { return (root || document).querySelector(sel); }
  function els(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

  function escapeHtml(value) {
    return String(value === null || value === undefined ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function toast(message) {
    var box = $('toast');
    box.textContent = message;
    box.hidden = false;
    clearTimeout(toast._timer);
    toast._timer = setTimeout(function () { box.hidden = true; }, 5000);
  }

  async function api(path, options) {
    var opts = Object.assign({ headers: {} }, options || {});
    opts.headers['Content-Type'] = 'application/json';
    if (TOKEN) opts.headers.Authorization = 'Bearer ' + TOKEN;
    if (opts.body && typeof opts.body !== 'string') opts.body = JSON.stringify(opts.body);

    var response = await fetch(BRIDGE + path, opts);
    var payload = null;
    try { payload = await response.json(); } catch (e) { payload = null; }

    if (!response.ok) {
      var detail = payload && payload.error ? payload.error : 'code ' + response.status;
      throw new Error(detail);
    }
    return payload;
  }

  // Boîtes de dialogue Windows, fournies par la fenêtre hôte.
  var pending = new Map();
  var nextId = 1;

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
      var data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
      var resolve = pending.get(data && data.id);
      if (!resolve) return;
      pending.delete(data.id);
      resolve(data.payload);
    });
  }

  function askHost(type, extra) {
    if (!(window.chrome && window.chrome.webview)) {
      return Promise.resolve({ error: 'sélecteur indisponible hors application' });
    }
    var id = 'req' + nextId++;
    return new Promise(function (resolve) {
      pending.set(id, resolve);
      window.chrome.webview.postMessage(JSON.stringify(Object.assign({ type: type, id: id }, extra || {})));
      setTimeout(function () { pending.delete(id); }, 300000);
    });
  }

  function pickFolder() {
    return askHost('selectFolder').then(function (r) { return r && r.folder ? r.folder : null; });
  }

  function pickFile(filter) {
    return askHost('selectFile', { filter: filter }).then(function (r) { return r && r.file ? r.file : null; });
  }

  // ── Icônes ────────────────────────────────────────────────────────────
  function paintIcons(root) {
    els('[data-icon]', root).forEach(function (node) {
      var name = node.getAttribute('data-icon');
      var url = 'url("icons/' + name + '.png")';
      node.style.webkitMaskImage = url;
      node.style.maskImage = url;
      node.setAttribute('aria-hidden', 'true');
    });
  }

  // ── Navigation ────────────────────────────────────────────────────────
  function show(view) {
    el('.app').setAttribute('data-accent', view);
    els('.view').forEach(function (section) { section.hidden = section.id !== 'view-' + view; });
    els('.nav-item').forEach(function (item) {
      if (item.dataset.view === view) item.setAttribute('aria-current', 'page');
      else item.removeAttribute('aria-current');
    });
    var meta = VIEWS[view] || ['Pinel', ''];
    $('view-title').textContent = meta[0];
    $('view-subtitle').textContent = meta[1];
    if (view === 'dossiers') loadSettings();
    if (view === 'journal') loadJournal();
  }

  // ── Rendu ─────────────────────────────────────────────────────────────
  function kpis(target, entries) {
    $(target).innerHTML = entries.map(function (entry) {
      return '<div class="kpi"><div class="label">' + escapeHtml(entry[0]) +
        '</div><div class="value">' + escapeHtml(entry[1]) + '</div></div>';
    }).join('');
  }

  function renderFiles(files) {
    var body = $('tbody-fichiers');
    if (!files.length) {
      body.innerHTML = '<tr><td colspan="4" class="empty">Aucun fichier trouvé dans les dossiers de travail.</td></tr>';
      return;
    }
    body.innerHTML = files.map(function (file) {
      var known = file.format && file.format !== 'INCONNU';
      return '<tr><td>' + escapeHtml(file.name) + '</td>' +
        '<td><span class="badge ' + (known ? 'ok' : 'warn') + '">' + escapeHtml(file.format) + '</span></td>' +
        '<td class="num">' + escapeHtml(file.sizeKb) + '</td>' +
        '<td class="path">' + escapeHtml(file.dir) + '</td></tr>';
    }).join('');
    $('badge-fichiers').textContent = files.length;
  }

  // ── Chantier 1 ────────────────────────────────────────────────────────
  async function addFolderAndScan() {
    var folder = await pickFolder();
    if (!folder) return;
    await api('/api/reglages/dossier', { method: 'POST', body: { chemin: folder } });
    toast('Dossier ajouté : ' + folder);
    await scan();
  }

  async function scan() {
    var result = await api('/api/scanner', { method: 'POST' });
    renderFiles(result.fichiers || []);
    kpis('kpi-moulinette', [['Fichiers', result.total || 0], ['Lignes lues', '—'], ['IPP uniques', '—'], ['Conflits', '—']]);
    toast(result.total + ' fichier(s) détecté(s).');
  }

  async function process() {
    toast('Traitement en cours…');
    var totals = await api('/api/traiter', { method: 'POST' });
    kpis('kpi-moulinette', [
      ['Fichiers traités', totals.filesProcessed],
      ['Lignes lues', totals.linesValid],
      ['IPP uniques', totals.ippUnique],
      ['Conflits', totals.collisions]
    ]);
    $('badge-conflits').textContent = totals.collisions;
    toast('Traitement terminé : ' + totals.linesValid + ' ligne(s).');
  }

  async function exportCsv() {
    var result = await api('/api/export-csv', { method: 'POST' });
    var files = result.fichiers || [];
    var raw = files.filter(function (f) { return f.brut; }).length;
    toast(files.length + ' CSV écrit(s) dans ' + result.dossier +
      (raw ? ' - ' + raw + ' sans descriptif, exporté(s) en ligne brute' : ''));
  }

  // ── Chantier 2 ────────────────────────────────────────────────────────
  async function buildEpisodes() {
    var payload = {
      toleranceJours: parseInt($('episode-tolerance').value, 10) || 0,
      ruptureUm: $('episode-um').checked,
      ruptureSite: $('episode-site').checked,
      fermetureAnnuelle: $('episode-annee').checked
    };
    var result = await api('/api/episodes', { method: 'POST', body: payload });

    kpis('kpi-episodes', [
      ['Venues lues', result.venues],
      ['Épisodes', result.episodes],
      ['Durée moyenne', result.dureeMoyenne + ' j'],
      ['Durée > 1 jour', result.superieurs1Jour]
    ]);

    var body = $('tbody-episodes');
    if (!result.apercu || !result.apercu.length) {
      var reason = (result.fichiersIgnores && result.fichiersIgnores.length)
        ? 'Descriptif de format insuffisant pour : ' + result.fichiersIgnores.join(', ') +
          '. Déposez le descriptif officiel dans le dossier des formats.'
        : 'Aucune venue trouvée. Traitez d\'abord un lot RAA ou RPS.';
      body.innerHTML = '<tr><td colspan="8" class="empty">' + escapeHtml(reason) + '</td></tr>';
      return;
    }

    body.innerHTML = result.apercu.map(function (e) {
      return '<tr><td class="mono">' + escapeHtml(e.id) + '</td><td>' + escapeHtml(e.ipp) + '</td>' +
        '<td>' + escapeHtml(e.um) + '</td><td>' + escapeHtml(e.site) + '</td>' +
        '<td>' + escapeHtml(e.debut) + '</td><td>' + escapeHtml(e.fin) + '</td>' +
        '<td class="num">' + escapeHtml(e.venues) + '</td>' +
        '<td class="num">' + escapeHtml(e.duree) + '</td></tr>';
    }).join('');

    toast(result.episodes + ' épisode(s) - règle : ' + result.regle);
  }

  // ── Chantier 3 ────────────────────────────────────────────────────────
  async function loadStructure() {
    var file = await pickFile('Structure|*.csv;*.tsv;*.txt|Tous les fichiers|*.*');
    if (!file) return;
    var result = await api('/api/structure', { method: 'POST', body: { fichier: file } });
    var summary = result.summary || {};
    var sectors = summary.bySectorType || {};

    var SECTOR_LABELS = {
      G: 'Adulte', I: 'Infanto-juvénile', D: 'UMD', P: 'UHSA', Z: 'Intersectoriel'
    };

    var rows = Object.keys(sectors).map(function (key) {
      var label = SECTOR_LABELS[key] || 'Non renseigné';
      return '<tr><td><span class="badge">' + escapeHtml(key) + '</span> ' + escapeHtml(label) +
        '</td><td class="num">' + escapeHtml(sectors[key]) + '</td></tr>';
    }).join('');

    $('card-structure').innerHTML =
      '<h3>' + escapeHtml(result.filename) + '</h3>' +
      '<p class="hint">Colonnes reconnues : ' + escapeHtml((result.headers || []).join(', ')) + '</p>' +
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

    var exportButton = $('btn-structure-export');
    if (exportButton) {
      exportButton.addEventListener('click', guard(async function () {
        var out = await api('/api/structure/export', { method: 'POST', body: { fichier: file } });
        toast(out.lignes + ' unité(s) exportée(s) vers ' + out.sortie);
      }));
    }
    toast('Structure chargée.');
  }

  // ── FICHCOMP ──────────────────────────────────────────────────────────
  async function cleanWorkbook() {
    var file = await pickFile('Classeur Excel|*.xlsx;*.xlsm|Tous les fichiers|*.*');
    if (!file) return;
    var result = await api('/api/fichcomp/nettoyer', { method: 'POST', body: { fichier: file } });
    $('card-fichcomp').innerHTML =
      '<h3>Classeur nettoyé</h3>' +
      '<p class="path">' + escapeHtml(result.sortie) + '</p>' +
      '<div class="grid cols-4">' +
      '<div class="kpi"><div class="label">Lignes lues</div><div class="value">' + result.lignesLues + '</div></div>' +
      '<div class="kpi"><div class="label">En-têtes retirés</div><div class="value">' + result.lignesRetirees + '</div></div>' +
      '<div class="kpi"><div class="label">Dates complétées</div><div class="value">' + result.datesCompletees + '</div></div>' +
      '</div>';
    toast('Classeur nettoyé.');
  }

  async function checkFichcomp() {
    var file = await pickFile('Fichier complémentaire|*.txt|Tous les fichiers|*.*');
    if (!file) return;
    var result = await api('/api/fichcomp/controler', {
      method: 'POST',
      body: { fichier: file, type: $('fichcomp-type').value }
    });

    var rows = (result.anomalies || []).map(function (issue) {
      return '<tr><td class="num">' + issue.lineNumber + '</td><td>' + escapeHtml(issue.message) + '</td></tr>';
    }).join('');

    $('card-fichcomp').innerHTML =
      '<h3>' + escapeHtml(result.type) + '</h3>' +
      '<p class="hint">' + result.lignes + ' ligne(s) contrôlée(s).</p>' +
      (result.conforme
        ? '<div class="note">Aucune anomalie détectée.</div>'
        : '<div class="table-wrap"><table><thead><tr><th class="num">Ligne</th><th>Anomalie</th></tr></thead><tbody>' +
          rows + '</tbody></table></div>');
    toast(result.conforme ? 'Fichier conforme.' : 'Anomalies détectées.');
  }

  // ── Identitovigilance ─────────────────────────────────────────────────
  async function loadCollisions() {
    var list = await api('/api/collisions');
    var body = $('tbody-collisions');
    $('badge-conflits').textContent = list.length;

    if (!list.length) {
      body.innerHTML = '<tr><td colspan="4" class="empty">Aucun conflit détecté.</td></tr>';
      return;
    }

    body.innerHTML = list.map(function (entry) {
      var dates = (entry.options || []).map(function (o) {
        return escapeHtml(o.ddn) + ' (' + o.occurrences + ')';
      }).join(', ');
      return '<tr><td class="mono">' + escapeHtml(entry.ipp) + '</td>' +
        '<td class="mono">' + escapeHtml(entry.pivot) + '</td>' +
        '<td>' + dates + '</td>' +
        '<td class="num">' + escapeHtml(entry.total) + '</td></tr>';
    }).join('');
  }

  // ── Contrôles ─────────────────────────────────────────────────────────
  async function runChecks() {
    var result = await api('/api/controles', { method: 'POST' });
    var summary = result.synthese || {};
    var anomalies = result.anomalies || [];

    var counters = Object.keys(summary).map(function (level) {
      return '<div class="kpi"><div class="label">' + escapeHtml(level) + '</div><div class="value">' +
        summary[level] + '</div></div>';
    }).join('');

    var rows = anomalies.slice(0, 200).map(function (finding) {
      return '<tr><td>' + escapeHtml(finding.sourceFile || '') + '</td>' +
        '<td class="num">' + escapeHtml(finding.lineNumber || '') + '</td>' +
        '<td>' + escapeHtml(finding.code || '') + '</td>' +
        '<td>' + escapeHtml(finding.message || '') + '</td></tr>';
    }).join('');

    $('card-controles').innerHTML =
      '<h3>Résultat</h3><div class="grid cols-4">' + counters + '</div>' +
      (anomalies.length
        ? '<div class="table-wrap" style="margin-top:var(--s3)"><table><thead><tr><th>Fichier</th>' +
          '<th class="num">Ligne</th><th>Code</th><th>Anomalie</th></tr></thead><tbody>' + rows + '</tbody></table></div>'
        : '<div class="note" style="margin-top:var(--s3)">Aucune anomalie sur le lot.</div>');
    toast(anomalies.length + ' anomalie(s).');
  }

  // ── Réglages ──────────────────────────────────────────────────────────
  async function loadSettings() {
    var settings = await api('/api/reglages');
    var body = $('tbody-dossiers');

    body.innerHTML = (settings.dossiers || []).length
      ? settings.dossiers.map(function (folder) {
          return '<tr><td class="path">' + escapeHtml(folder) + '</td>' +
            '<td style="text-align:right"><button class="btn ghost" data-remove="' + escapeHtml(folder) +
            '" type="button">Retirer</button></td></tr>';
        }).join('')
      : '<tr><td colspan="2" class="empty">Aucun dossier ajouté. Seul l\'espace de travail local est autorisé.</td></tr>';

    els('[data-remove]', body).forEach(function (button) {
      button.addEventListener('click', async function () {
        await api('/api/reglages/dossier', { method: 'DELETE', body: { chemin: button.dataset.remove } });
        toast('Dossier retiré.');
        loadSettings();
      });
    });

    $('dossier-sortie').textContent = settings.dossierSortie || '—';
    $('dossier-formats').textContent = settings.dossierFormats || '—';
  }

  async function loadJournal() {
    var audit = await api('/api/audit');
    $('chemin-audit').textContent = audit.chemin || '—';
    var entries = await api('/api/journal');
    if (entries && entries.length) {
      $('journal').textContent = entries.map(function (e) {
        return typeof e === 'string' ? e : JSON.stringify(e);
      }).join('\n');
    }
  }

  // ── Câblage ───────────────────────────────────────────────────────────
  function guard(handler) {
    return function () {
      Promise.resolve()
        .then(handler)
        .catch(function (error) { toast('Erreur : ' + error.message); });
    };
  }

  function wire() {
    els('.nav-item').forEach(function (item) {
      item.addEventListener('click', function () { show(item.dataset.view); });
    });

    $('btn-reset').addEventListener('click', guard(async function () {
      await api('/api/reinitialiser', { method: 'POST' });
      renderFiles([]);
      $('badge-fichiers').textContent = '0';
      $('badge-conflits').textContent = '0';
      toast('Session réinitialisée.');
    }));

    $('btn-ajouter-dossier').addEventListener('click', guard(addFolderAndScan));
    $('btn-scanner').addEventListener('click', guard(scan));
    $('btn-traiter').addEventListener('click', guard(process));
    $('btn-export-csv').addEventListener('click', guard(exportCsv));
    $('btn-episodes').addEventListener('click', guard(buildEpisodes));
    $('btn-structure').addEventListener('click', guard(loadStructure));
    $('btn-fichcomp-nettoyer').addEventListener('click', guard(cleanWorkbook));
    $('btn-fichcomp-controler').addEventListener('click', guard(checkFichcomp));
    $('btn-collisions').addEventListener('click', guard(loadCollisions));
    $('btn-export-identite').addEventListener('click', guard(async function () {
      var result = await api('/api/export-identite', { method: 'POST' });
      toast(result.lignes + ' ligne(s) écrite(s) dans ' + result.sortie);
    }));
    $('btn-controles').addEventListener('click', guard(runChecks));
    $('btn-dossier-ajouter').addEventListener('click', guard(async function () {
      var folder = await pickFolder();
      if (!folder) return;
      await api('/api/reglages/dossier', { method: 'POST', body: { chemin: folder } });
      loadSettings();
      toast('Dossier ajouté.');
    }));
    $('btn-dossier-sortie').addEventListener('click', guard(async function () {
      var folder = await pickFolder();
      if (!folder) return;
      await api('/api/reglages/sortie', { method: 'POST', body: { chemin: folder } });
      loadSettings();
      toast('Dossier de sortie mis à jour.');
    }));
    $('btn-dossier-formats').addEventListener('click', guard(async function () {
      var folder = await pickFolder();
      if (!folder) return;
      var result = await api('/api/reglages/formats', { method: 'POST', body: { chemin: folder } });
      loadSettings();
      toast(result.descriptifs + ' format(s) décrits.');
    }));
  }

  function start() {
    paintIcons(document);
    wire();
    show('moulinette');
    kpis('kpi-moulinette', [['Fichiers', 0], ['Lignes lues', 0], ['IPP uniques', 0], ['Conflits', 0]]);
    kpis('kpi-episodes', [['Venues lues', 0], ['Épisodes', 0], ['Durée moyenne', '0 j'], ['Durée > 1 jour', 0]]);
    loadSettings().catch(function () { /* le service n'est pas encore prêt */ });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
