/*
 * Pinel - écran "Traçabilité" : chemin du journal d'audit et journal de
 * session. Se recharge à chaque affichage de l'écran, via l'événement
 * "pinel:view" diffusé par navigation.js.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  async function loadJournal() {
    var audit = await api.call('/api/audit');
    dom.$('chemin-audit').textContent = audit.chemin || '—';
    var entries = await api.call('/api/journal');
    if (entries && entries.length) {
      dom.$('journal').textContent = entries.map(function (e) {
        return typeof e === 'string' ? e : JSON.stringify(e);
      }).join('\n');
    }
  }

  window.addEventListener('pinel:view', function (event) {
    if (event.detail === 'journal') loadJournal().catch(function (e) { dom.toast('Erreur : ' + e.message); });
  });
})();
