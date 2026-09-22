/*
 * Pinel - navigation entre écrans, avec le patron d'accessibilité ARIA
 * "tabs" : chaque entrée du menu est un onglet (role="tab"), chaque écran
 * est son panneau (role="tabpanel"). L'index de tabulation suit l'onglet
 * actif (tabindex roulant) et les flèches Haut/Bas/Origine/Fin déplacent le
 * focus, conformément au référentiel WAI-ARIA pour un ensemble d'onglets en
 * orientation verticale.
 *
 * Diffuse un événement "pinel:view" à chaque changement d'écran : les
 * modules qui doivent recharger leurs données à l'affichage (réglages,
 * journal) s'y abonnent eux-mêmes, sans que ce fichier ait à les connaître.
 *
 * Expose window.Pinel.nav. Dépend de window.Pinel.dom (core-dom.js).
 */
(function () {
  'use strict';

  window.Pinel = window.Pinel || {};
  var dom = window.Pinel.dom;

  var VIEWS = {
    moulinette: ['Conversion des formats ATIH',
      'Cahier des charges DIM, point 1 - fichiers au format national convertis en CSV exploitable'],
    episodes: ['Épisodes de prise en charge',
      'Cahier des charges DIM, point 2 - regroupement des journées consécutives en ambulatoire'],
    structure: ['Structure du GHT',
      'Cahier des charges DIM, point 3 - lecture et contrôle du fichier de structure FICOM'],
    fichcomp: ['Transports et fichiers complémentaires',
      "Anciennement moulinette à transports : nettoyage des classeurs transports et contrôle des fichiers " +
      'complémentaires avant transmission'],
    identito: ['Identitovigilance',
      'Identifiants patients porteurs de plusieurs dates de naissance'],
    controles: ['Contrôles qualité',
      'Anomalies relevées avant transmission aux tutelles'],
    dossiers: ['Emplacements autorisés',
      'Répertoires que Pinel est autorisé à lire et à écrire'],
    journal: ['Traçabilité',
      "Journal des opérations sensibles, registre au titre de l'article 30 du RGPD"],
    apropos: ['À propos',
      'Version, licences et attribution des ressources utilisées par Pinel']
  };

  function paintIcons(root) {
    dom.els('[data-icon]', root).forEach(function (node) {
      dom.paintIcon(node, node.getAttribute('data-icon'));
    });
  }

  function tabs() { return dom.els('[role="tab"]'); }

  function show(view) {
    dom.el('.app').setAttribute('data-accent', view);
    dom.els('.view').forEach(function (section) { section.hidden = section.id !== 'view-' + view; });
    tabs().forEach(function (item) {
      var active = item.dataset.view === view;
      item.setAttribute('aria-selected', active ? 'true' : 'false');
      item.tabIndex = active ? 0 : -1;
    });
    var meta = VIEWS[view] || ['Pinel', ''];
    dom.$('view-title').textContent = meta[0];
    dom.$('view-subtitle').textContent = meta[1];
    window.dispatchEvent(new CustomEvent('pinel:view', { detail: view }));
  }

  function activate(index, list) {
    var target = list[(index + list.length) % list.length];
    show(target.dataset.view);
    target.focus();
  }

  function onKeydown(event) {
    var list = tabs();
    var index = list.indexOf(event.target);
    if (index === -1) return;
    if (event.key === 'ArrowDown') { event.preventDefault(); activate(index + 1, list); }
    else if (event.key === 'ArrowUp') { event.preventDefault(); activate(index - 1, list); }
    else if (event.key === 'Home') { event.preventDefault(); activate(0, list); }
    else if (event.key === 'End') { event.preventDefault(); activate(list.length - 1, list); }
  }

  function wire() {
    tabs().forEach(function (item) {
      item.addEventListener('click', function () { show(item.dataset.view); });
    });
    dom.el('[role="tablist"]').addEventListener('keydown', onKeydown);
  }

  window.Pinel.nav = { show: show, paintIcons: paintIcons, wire: wire };
})();
