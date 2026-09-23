/*
 * Pinel - utilitaires DOM partagés par tous les écrans : sélection,
 * échappement HTML, rendu des cartes KPI et notification (toast).
 *
 * Expose window.Pinel.dom. Chargé en premier : les autres fichiers y
 * puisent leurs raccourcis $/el/els.
 */
(function () {
  'use strict';

  window.Pinel = window.Pinel || {};

  function $(id) { return document.getElementById(id); }
  function el(sel, root) { return (root || document).querySelector(sel); }
  function els(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

  function escapeHtml(value) {
    return String(value === null || value === undefined ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // Fragment HTML d'une rangée de cartes KPI, réutilisé partout où un
  // écran affiche un jeu de compteurs (moulinette, épisodes, contrôles).
  function kpiCards(entries) {
    return entries.map(function (entry) {
      return '<div class="kpi"><div class="label">' + escapeHtml(entry[0]) +
        '</div><div class="value">' + escapeHtml(entry[1]) + '</div></div>';
    }).join('');
  }

  function kpis(targetId, entries) {
    $(targetId).innerHTML = kpiCards(entries);
  }

  // Pose un glyphe Phosphor en masque CSS sur un élément .i ; le fichier
  // SVG doit figurer dans la liste de tools/vendor_assets.py.
  function paintIcon(node, name) {
    var url = 'url("icons/' + name + '.svg")';
    node.style.webkitMaskImage = url;
    node.style.maskImage = url;
    node.setAttribute('aria-hidden', 'true');
  }

  // Notification annoncée par le lecteur d'écran via role="status" : le
  // texte est posé avant de retirer l'attribut hidden, ce qui garantit
  // l'annonce même si le message reprend le précédent mot pour mot. Une
  // erreur reste affichée plus longtemps, le temps d'être lue en entier.
  function toast(message, kind) {
    var box = $('toast');
    var error = kind === 'error';
    box.hidden = true;
    box.classList.toggle('error', error);
    box.innerHTML = '<span class="i"></span><span></span>';
    paintIcon(box.firstChild, error ? 'warning-circle' : 'check-circle');
    box.lastChild.textContent = message;
    box.hidden = false;
    clearTimeout(toast._timer);
    toast._timer = setTimeout(function () { box.hidden = true; }, error ? 9000 : 5000);
  }

  window.Pinel.dom = { $: $, el: el, els: els, escapeHtml: escapeHtml, kpiCards: kpiCards, kpis: kpis, paintIcon: paintIcon, toast: toast };
})();
