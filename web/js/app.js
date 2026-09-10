/*
 * Pinel - amorçage de l'interface : peint les icônes, câble la navigation
 * et le bouton de réinitialisation, affiche l'écran par défaut.
 *
 * Chargé en dernier : tous les écrans (vue-*.js) ont déjà câblé leurs
 * propres boutons à ce stade.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;
  var nav = window.Pinel.nav;

  function wireReset() {
    dom.$('btn-reset').addEventListener('click', api.guard(async function () {
      await api.call('/api/reinitialiser', { method: 'POST' });
      window.Pinel.moulinette.reset();
      dom.toast('Session réinitialisée.');
    }));
  }

  function syncAproposVersion() {
    var source = dom.$('version');
    var target = dom.$('apropos-version');
    if (source && target) target.textContent = source.textContent;
  }

  function start() {
    nav.paintIcons(document);
    nav.wire();
    wireReset();
    syncAproposVersion();
    nav.show('moulinette');

    // Amorce discrète des réglages au démarrage : si le service local n'est
    // pas encore prêt, l'échec est silencieux ici, l'écran "Emplacements
    // autorisés" retentera de lui-même à l'affichage (voir vue-reglages.js).
    window.Pinel.reglages.load().catch(function () { /* le service n'est pas encore prêt */ });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
