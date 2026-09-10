/*
 * Pinel - écran "Identitovigilance" : identifiants patients porteurs de
 * plusieurs dates de naissance, export de la table de correspondance.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  async function loadCollisions() {
    var list = await api.call('/api/collisions');
    var body = dom.$('tbody-collisions');
    dom.$('badge-conflits').textContent = list.length;

    if (!list.length) {
      body.innerHTML = '<tr><td colspan="4" class="empty">Aucun conflit détecté.</td></tr>';
      dom.toast('Aucun conflit détecté.');
      return;
    }

    body.innerHTML = list.map(function (entry) {
      var dates = (entry.options || []).map(function (o) {
        return dom.escapeHtml(o.ddn) + ' (' + o.occurrences + ')';
      }).join(', ');
      return '<tr><td class="mono">' + dom.escapeHtml(entry.ipp) + '</td>' +
        '<td class="mono">' + dom.escapeHtml(entry.pivot) + '</td>' +
        '<td>' + dates + '</td>' +
        '<td class="num">' + dom.escapeHtml(entry.total) + '</td></tr>';
    }).join('');
    dom.toast(list.length + ' conflit(s) détecté(s).');
  }

  function wire() {
    dom.$('btn-collisions').addEventListener('click', api.guard(loadCollisions));
    dom.$('btn-export-identite').addEventListener('click', api.guard(async function () {
      var result = await api.call('/api/export-identite', { method: 'POST' });
      dom.toast(result.lignes + ' ligne(s) écrite(s) dans ' + result.sortie);
    }));
  }

  wire();
})();
